import { HubConnection, HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import { NOTIFICATIONS_BASE_URL } from "./config";

let connection: HubConnection | null = null;

export function getOrderHubConnection(): HubConnection {
  if (!connection) {
    connection = new HubConnectionBuilder()
      .withUrl(`${NOTIFICATIONS_BASE_URL}/orderhub`)
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Information)
      .build();
  }

  return connection;
}
