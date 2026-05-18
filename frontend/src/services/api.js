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

export async function getHomeAssistantStatus() {
  const { data } = await api.get("/api/home/status");
  return data;
}

export async function getHomeAssistantStates() {
  const { data } = await api.get("/api/home/states");
  return data;
}

export async function getHomeAssistantState(entityId) {
  const { data } = await api.get("/api/home/state", {
    params: { entityId },
  });

  return data;
}

export async function turnOnHomeEntity(entityId) {
  const { data } = await api.post("/api/home/turn-on", { entityId });
  return data;
}

export async function turnOffHomeEntity(entityId) {
  const { data } = await api.post("/api/home/turn-off", { entityId });
  return data;
}

export async function toggleHomeEntity(entityId) {
  const { data } = await api.post("/api/home/toggle", { entityId });
  return data;
}

export async function setHomeLightBrightness(entityId, brightness) {
  const { data } = await api.post("/api/home/brightness", { entityId, brightness });
  return data;
}

export async function getNetworkStatus() {
  const { data } = await api.get("/api/network/status");
  return data;
}

export async function getNetworkDevices() {
  const { data } = await api.get("/api/network/devices");
  return data;
}

export async function checkNetwork() {
  const { data } = await api.post("/api/network/check");
  return data;
}

export async function getUnknownNetworkDevices() {
  const { data } = await api.get("/api/network/unknown");
  return data;
}

export async function resolveUnknownNetworkDevice(payload) {
  const { data } = await api.post("/api/network/unknown/resolve", payload);
  return data;
}

export async function getTodayBriefing() {
  const { data } = await api.get("/api/today");
  return data;
}

export async function getWeatherReport() {
  const { data } = await api.get("/api/weather/report");
  return data;
}

export async function getIntegrationsStatus() {
  const { data } = await api.get("/api/integrations/status");
  return data;
}

export async function getCalendarBriefing() {
  const { data } = await api.get("/api/integrations/calendar/briefing");
  return data;
}

export async function getGmailBriefing() {
  const { data } = await api.get("/api/integrations/gmail/briefing");
  return data;
}

export async function getSpotifyStatus() {
  const { data } = await api.get("/api/spotify/status");
  return data;
}

export async function getSpotifyCurrent() {
  const { data } = await api.get("/api/spotify/current");
  return data;
}

export async function getSpotifyPlaylists() {
  const { data } = await api.get("/api/spotify/playlists");
  return data;
}

export async function playSpotify(query = "", contextUri = "") {
  const { data } = await api.post("/api/spotify/play", { query, contextUri });
  return data;
}

export async function pauseSpotify() {
  const { data } = await api.post("/api/spotify/pause");
  return data;
}

export async function nextSpotify() {
  const { data } = await api.post("/api/spotify/next");
  return data;
}

export async function previousSpotify() {
  const { data } = await api.post("/api/spotify/previous");
  return data;
}

export async function setSpotifyVolume(volumePercent) {
  const { data } = await api.post("/api/spotify/volume", { volumePercent });
  return data;
}
