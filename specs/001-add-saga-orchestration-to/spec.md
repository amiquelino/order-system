# Feature Specification: Saga Orchestration for Order Fulfillment

**Feature Directory**: `specs/001-add-saga-orchestration-to/`

**Created**: 2026-10-24

**Status**: Draft

**Input**: User description: "Add saga orchestration to the order fulfillment workflow. Today, order fulfillment is a single-step payment process embedded in CreateOrderCommandHandler/RetryPaymentCommandHandler: charge payment, retry via a RabbitMQ delay queue up to 3 times, and if all retries fail the order is just stuck in PaymentFailed forever with no compensation. Add a second step - stock reservation - so there's a real multi-step workflow with compensation: reserve stock -> charge payment (existing retry logic) -> confirm. If payment retries are exhausted, compensate: release the reserved stock, then cancel the order (reusing the existing Cancelled status - no new OrderStatus values). [...] Manual cancellation (CancelOrderCommandHandler) must also release any reserved stock as compensation. No frontend or NotificationService changes needed since no new OrderStatus values are introduced."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Permanently-failed orders resolve instead of getting stuck (Priority: P1)

As a customer, if my order's payment can't go through even after the system
has retried it, I want my order to end up in a final, resolved state
(cancelled, with anything held for it released) instead of sitting
indefinitely in a failed state with no path forward and no indication
anything will ever happen to it again.

**Why this priority**: This is the defect that motivates the whole feature.
Today, an order whose payment permanently fails is simply abandoned mid-flow
— it is the single biggest gap in the current fulfillment process and the
one with the most direct, visible impact on a real customer.

**Independent Test**: Place an order whose payment amount always declines
(exhausting every retry attempt), then verify that — with no manual
intervention — the order's status becomes Cancelled and nothing remains held
for it.

**Acceptance Scenarios**:

1. **Given** a new order whose payment always declines, **When** every
   payment retry attempt has been exhausted, **Then** the order's status
   becomes Cancelled and anything reserved for it is released.
2. **Given** an order whose payment succeeds on a retry (not exhausted),
   **When** payment is confirmed, **Then** the order's reservation is
   retained as fulfilled and nothing is released.

---

### User Story 2 - Manual cancellation also releases what was held for the order (Priority: P2)

As a customer or operator, when I cancel an order myself before it's
confirmed, I want anything that was reserved for it to be released
immediately, so it isn't held unnecessarily once the order is no longer
going anywhere.

**Why this priority**: Without this, the two ways an order can end up
cancelled — automatically, after exhausted payment retries (User Story 1),
or manually — would behave inconsistently: one releases what was held for
the order, the other wouldn't. That inconsistency defeats the point of
adding compensation in the first place.

**Independent Test**: Create an order (so items are reserved for it), cancel
it manually before its payment resolves, and verify the reservation is
released and the order's status becomes Cancelled.

**Acceptance Scenarios**:

1. **Given** a new order with items reserved for it, **When** it is
   cancelled manually before payment completes, **Then** its reservation is
   released and the order's status becomes Cancelled.
2. **Given** an order that has already reached the Confirmed status,
   **When** a cancellation is attempted, **Then** the system rejects it
   (existing behavior, unchanged by this feature) and no reservation
   changes occur.

---

### Edge Cases

- What happens if a scheduled payment-retry outcome arrives for an order
  that was already cancelled manually in the meantime? The order has
  already left the fulfillment flow for this feature's purposes — that
  retry outcome has no further effect on its status or its (already
  released) reservation.
- What happens when cancellation is requested for an order whose items were
  never actually reserved (e.g., cancelled before the reservation step
  completed)? The system handles this without error — there is simply
  nothing to release.
- What prevents the same order's reservation from being released twice, if
  both the automatic (exhausted-retry) and manual cancellation paths were
  ever triggered for the same order? The system treats releasing an
  already-released reservation as a no-op, not an error.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST reserve an order's items at the time the order is
  created, before any payment attempt is made for it.
- **FR-002**: System MUST only attempt to charge payment for an order whose
  item reservation has succeeded.
- **FR-003**: System MUST release an order's reserved items once its
  payment has permanently failed (every retry attempt exhausted).
- **FR-004**: System MUST transition an order whose payment has permanently
  failed to the Cancelled status automatically, with no manual action
  required.
- **FR-005**: System MUST release an order's reserved items when the order
  is cancelled directly by a customer or operator, in addition to the
  automatic case in FR-003.
- **FR-006**: System MUST NOT release the same order's reserved items more
  than once, regardless of which path (automatic or manual) triggers the
  release.
- **FR-007**: System MUST retain an order's reserved items (not release
  them) once its payment is confirmed.
- **FR-008**: System MUST continue to reject cancellation of an order that
  has already reached the Confirmed status (existing behavior, unchanged by
  this feature).
- **FR-009**: System MUST handle a cancellation request for an order whose
  items were never reserved without error.

### Key Entities

- **Order**: The existing customer-facing entity; its shape and status
  values are unchanged by this feature (Created, PaymentPending,
  PaymentFailed, Confirmed, Cancelled). Cancelled now also serves as the
  terminal outcome for an order whose payment permanently failed, not only
  for direct cancellation.
- **Stock Reservation**: Represents the hold an order places on its ordered
  items. Tracked independently of the order itself so that it can be
  released exactly once, no matter which path — automatic exhausted-retry
  compensation, or manual cancellation — triggers that release.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of orders whose payment permanently fails reach the
  Cancelled status automatically, with zero requiring manual intervention
  to resolve.
- **SC-002**: 100% of cancelled orders — whether cancelled automatically or
  manually — have anything reserved for them released as part of that same
  cancellation; none remain held afterward.
- **SC-003**: No order's reservation is ever released more than once,
  including when both the automatic and manual cancellation paths could
  plausibly be triggered for the same order.
- **SC-004**: Orders that pay successfully (on the first attempt or after a
  retry) are unaffected in outcome or user-visible behavior by the addition
  of the reservation step.

## Assumptions

- Stock reservation in this iteration always succeeds — there is no
  inventory capacity limit being enforced yet. Modeling a rejected
  reservation (insufficient stock) is out of scope and left for a future
  iteration.
- The existing payment retry limit and timing are unchanged by this
  feature; this feature only adds what happens before the first payment
  attempt (reservation) and after the last retry is exhausted
  (compensation).
- Payment continues to be attempted as part of placing the order (the first
  attempt), consistent with current behavior; this feature does not change
  when or how payment is attempted, only what happens around it.
- Once an order reaches Cancelled by either path, it is terminal for this
  feature — nothing in this feature resurrects a cancelled order's
  reservation.
- No new customer-facing status values are introduced; the existing
  Cancelled status is reused for the exhausted-retry outcome, so no
  frontend or notification changes are required.
