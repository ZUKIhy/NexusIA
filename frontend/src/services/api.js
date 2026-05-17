import axios from "axios";

export const API_URL = import.meta.env.VITE_API_URL || "http://localhost:5000";

export const api = axios.create({ baseURL: API_URL });

export async function sendMessage(message) {
  const { data } = await api.post("/api/nexus/chat", { message });
  return data;
}

export async function getStatus() {
  const { data } = await api.get("/api/nexus/status");
  return data;
}

export async function getMemory() {
  const { data } = await api.get("/api/memory");
  return data;
}

export async function getTasks() {
  const { data } = await api.get("/api/tasks");
  return data;
}

export async function getConversationLogs() {
  const { data } = await api.get("/api/logs/conversations");
  return data;
}

export async function searchDocs(query) {
  const { data } = await api.get("/api/docs/search", {
    params: { q: query },
  });

  return data;
}

export async function getDocFile(path) {
  const { data } = await api.get("/api/docs/file", {
    params: { path },
  });

  return data;
}

export async function standardizeDoc(path) {
  const { data } = await api.post("/api/docs/standardize", { path });
  return data;
}

export async function generateDocChecklist(path) {
  const { data } = await api.post("/api/docs/checklist", { path });
  return data;
}

export async function generateChecklist(path) {
  return generateDocChecklist(path);
}

export async function getSystemHealth() {
  const { data } = await api.get("/api/system/health");
  return data;
}

export async function getProcedures() {
  const { data } = await api.get("/api/procedures");
  return data;
}

export async function getVoiceStatus() {
  const { data } = await api.get("/api/voice/status");
  return data;
}

export async function getComputerStatus() {
  const { data } = await api.get("/api/computer/status");
  return data;
}
