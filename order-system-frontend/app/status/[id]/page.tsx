"use client";

import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import { HubConnectionState } from "@microsoft/signalr";
import { API_BASE_URL } from "@/lib/config";
import { getOrderHubConnection } from "@/lib/signalr";

export default function OrderStatusPage() {
  const params = useParams<{ id: string }>();
  const orderId = params.id;
  const [status, setStatus] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let isMounted = true;

    async function fetchStatus() {
      try {
        const response = await fetch(`${API_BASE_URL}/orders/${orderId}`);
        if (!response.ok) {
          throw new Error(`Request failed with status ${response.status}`);
        }
        const data = await response.json();
        if (isMounted) setStatus(data.status);
      } catch (err) {
        if (isMounted) {
          setError(err instanceof Error ? err.message : "Failed to load order");
        }
      }
    }

    fetchStatus();

    const connection = getOrderHubConnection();

    function handleStatusChanged(updatedOrderId: string, newStatus: string) {
      if (updatedOrderId === orderId) {
        setStatus(newStatus);
      }
    }

    connection.on("OrderStatusChanged", handleStatusChanged);

    if (connection.state === HubConnectionState.Disconnected) {
      connection
        .start()
        .catch((err) =>
          setError(err instanceof Error ? err.message : "Failed to connect to hub")
        );
    }

    return () => {
      isMounted = false;
      connection.off("OrderStatusChanged", handleStatusChanged);
    };
  }, [orderId]);

  return (
    <main style={{ padding: "2rem", maxWidth: 640, margin: "0 auto" }}>
      <h1>Order Status</h1>
      <p>Order ID: {orderId}</p>
      {error && <p style={{ color: "red" }}>{error}</p>}
      <p>Status: {status ?? "Loading..."}</p>
    </main>
  );
}
