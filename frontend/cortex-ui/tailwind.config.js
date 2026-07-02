import cortexPreset from "./tailwind-preset.js";

/** @type {import('tailwindcss').Config} */
export default {
  content: ["./index.html", "./src/**/*.{js,ts,jsx,tsx}"],
  darkMode: "media",
  presets: [cortexPreset],
  theme: {
    extend: {},
  },
  plugins: [],
};
