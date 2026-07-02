import { BrowserRouter } from "react-router-dom";
import { AppShell } from "./routes/AppShell";

export default function App() {
  return (
    // Opt into the React Router v7 behaviors now — silences the v6 future-flag console warnings and keeps
    // routing forward-compatible. Safe here: the app uses absolute links (no relative splat-path resolution).
    <BrowserRouter future={{ v7_startTransition: true, v7_relativeSplatPath: true }}>
      <AppShell />
    </BrowserRouter>
  );
}
