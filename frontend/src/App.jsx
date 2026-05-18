import { NavLink, Route, Routes } from "react-router-dom";
import { Activity, Brain, CalendarDays, ClipboardCheck, ClipboardList, FileSearch, Home, HousePlug, Music2, Network, ScrollText, Settings, Terminal } from "lucide-react";
import Dashboard from "./pages/Dashboard.jsx";
import Logs from "./pages/Logs.jsx";
import SettingsPage from "./pages/Settings.jsx";
import System from "./pages/System.jsx";
import Memory from "./pages/Memory.jsx";
import Tasks from "./pages/Tasks.jsx";
import Docs from "./pages/Docs.jsx";
import Procedures from "./pages/Procedures.jsx";
import HomeAssistant from "./pages/HomeAssistant.jsx";
import NetworkMonitor from "./pages/NetworkMonitor.jsx";
import Today from "./pages/Today.jsx";
import Media from "./pages/Media.jsx";

export default function App() {
  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="brand">
          <div className="brand-core" />
          <div>
            <strong>N.E.X.U.S</strong>
            <span>Gabriel System</span>
          </div>
        </div>

        <nav>
          <NavItem to="/" icon={<Home size={18} />} label="Dashboard" />
          <NavItem to="/today" icon={<CalendarDays size={18} />} label="Today" />
          <NavItem to="/memory" icon={<Brain size={18} />} label="Memory" />
          <NavItem to="/docs" icon={<FileSearch size={18} />} label="Docs" />
          <NavItem to="/procedures" icon={<ClipboardCheck size={18} />} label="Procedures" />
          <NavItem to="/home-assistant" icon={<HousePlug size={18} />} label="Home Assistant" />
          <NavItem to="/media" icon={<Music2 size={18} />} label="Media" />
          <NavItem to="/network" icon={<Network size={18} />} label="Network" />
          <NavItem to="/tasks" icon={<ClipboardList size={18} />} label="Tasks" />
          <NavItem to="/logs" icon={<ScrollText size={18} />} label="Logs" />
          <NavItem to="/system" icon={<Terminal size={18} />} label="System" />
          <NavItem to="/settings" icon={<Settings size={18} />} label="Settings" />
        </nav>

        <div className="sidebar-footer">
          <Activity size={16} />
          <span>Online</span>
        </div>
      </aside>

      <main className="main">
        <Routes>
          <Route path="/" element={<Dashboard />} />
          <Route path="/today" element={<Today />} />
          <Route path="/memory" element={<Memory />} />
          <Route path="/docs" element={<Docs />} />
          <Route path="/procedures" element={<Procedures />} />
          <Route path="/home-assistant" element={<HomeAssistant />} />
          <Route path="/media" element={<Media />} />
          <Route path="/network" element={<NetworkMonitor />} />
          <Route path="/tasks" element={<Tasks />} />
          <Route path="/logs" element={<Logs />} />
          <Route path="/system" element={<System />} />
          <Route path="/settings" element={<SettingsPage />} />
        </Routes>
      </main>
    </div>
  );
}

function NavItem({ to, icon, label }) {
  return (
    <NavLink to={to} className={({ isActive }) => `nav-item ${isActive ? "active" : ""}`}>
      {icon}
      <span>{label}</span>
    </NavLink>
  );
}
