"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { API_BASE_URL } from "@/lib/config";

interface OrderItemForm {
  productName: string;
  quantity: number;
  unitPrice: number;
}

const emptyItem: OrderItemForm = { productName: "", quantity: 1, unitPrice: 0 };

export default function Home() {
  const router = useRouter();
  const [customerId, setCustomerId] = useState(() => crypto.randomUUID());
  const [items, setItems] = useState<OrderItemForm[]>([{ ...emptyItem }]);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  function updateItem(index: number, changes: Partial<OrderItemForm>) {
    setItems((prev) =>
      prev.map((item, i) => (i === index ? { ...item, ...changes } : item))
    );
  }

  function addItem() {
    setItems((prev) => [...prev, { ...emptyItem }]);
  }

  function removeItem(index: number) {
    setItems((prev) => prev.filter((_, i) => i !== index));
  }

  async function handleSubmit(event: React.FormEvent) {
    event.preventDefault();
    setError(null);
    setSubmitting(true);

    try {
      const response = await fetch(`${API_BASE_URL}/orders`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          customerId,
          items: items.map((item) => ({
            productName: item.productName,
            quantity: item.quantity,
            unitPrice: item.unitPrice,
            currency: "USD",
          })),
        }),
      });

      if (!response.ok) {
        throw new Error(`Request failed with status ${response.status}`);
      }

      const data = await response.json();
      router.push(`/status/${data.orderId}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to create order");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <main style={{ padding: "2rem", maxWidth: 640, margin: "0 auto" }}>
      <h1>Create Order</h1>

      <form onSubmit={handleSubmit}>
        <div style={{ marginBottom: "1rem" }}>
          <label htmlFor="customerId">Customer ID</label>
          <br />
          <input
            id="customerId"
            type="text"
            value={customerId}
            onChange={(e) => setCustomerId(e.target.value)}
            style={{ width: "100%" }}
          />
        </div>

        <h2>Items</h2>
        {items.map((item, index) => (
          <div
            key={index}
            style={{ display: "flex", gap: "0.5rem", marginBottom: "0.5rem" }}
          >
            <input
              type="text"
              placeholder="Product name"
              value={item.productName}
              onChange={(e) => updateItem(index, { productName: e.target.value })}
              required
            />
            <input
              type="number"
              placeholder="Quantity"
              min={1}
              value={item.quantity}
              onChange={(e) =>
                updateItem(index, { quantity: Number(e.target.value) })
              }
              required
            />
            <input
              type="number"
              placeholder="Unit price"
              min={0}
              step="0.01"
              value={item.unitPrice}
              onChange={(e) =>
                updateItem(index, { unitPrice: Number(e.target.value) })
              }
              required
            />
            <button
              type="button"
              onClick={() => removeItem(index)}
              disabled={items.length === 1}
            >
              Remove
            </button>
          </div>
        ))}

        <button type="button" onClick={addItem}>
          Add item
        </button>

        <div style={{ marginTop: "1.5rem" }}>
          <button type="submit" disabled={submitting}>
            {submitting ? "Creating order..." : "Create order"}
          </button>
        </div>
      </form>

      {error && <p style={{ color: "red" }}>{error}</p>}
    </main>
  );
}
