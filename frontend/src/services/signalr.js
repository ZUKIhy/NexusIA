import * as signalR from "@microsoft/signalr";
import { API_URL } from "./api";

export function createNexusConnection({ onLog, onActivated, onThinking, onSpeaking, onMemorySaved, onTaskCreated }) {
  const connection = new signalR.HubConnectionBuilder()
    .withUrl(`${API_URL}/hubs/nexus`)
    .withAutomaticReconnect()
    .build();

  connection.on("nexus:log", payload => onLog?.(payload));
  connection.on("nexus:activated", payload => onActivated?.(payload));
  connection.on("nexus:thinking", payload => onThinking?.(payload));
  connection.on("nexus:speaking", payload => onSpeaking?.(payload));
  connection.on("nexus:memory_saved", payload => onMemorySaved?.(payload));
  connection.on("nexus:task_created", payload => onTaskCreated?.(payload));

  return connection;
}
