import type { Config } from "tailwindcss";

const config: Config = {
  darkMode: "class",
  content: [
    "./app/**/*.{ts,tsx}",
    "./components/**/*.{ts,tsx}",
    "./lib/**/*.{ts,tsx}",
  ],
  theme: {
    extend: {
      colors: {
        // Calm & trustworthy: soft blue primary with a teal accent.
        brand: {
          DEFAULT: "#2563eb",
          dark: "#1d4ed8",
          light: "#eaf1ff",
          50: "#eff6ff",
          100: "#dbeafe",
          600: "#2563eb",
          700: "#1d4ed8",
        },
        accent: {
          DEFAULT: "#0ea5a4",
          dark: "#0b8281",
          light: "#e6fffb",
        },
      },
      fontFamily: {
        sans: ["var(--font-sans)", "system-ui", "sans-serif"],
      },
      borderRadius: {
        xl: "0.9rem",
        "2xl": "1.25rem",
        "3xl": "1.75rem",
      },
      backgroundImage: {
        // Very light blue→teal wash for hero / page background.
        hero: "linear-gradient(160deg, #eff6ff 0%, #e6fffb 100%)",
        "brand-gradient": "linear-gradient(135deg, #2563eb 0%, #0ea5a4 100%)",
      },
      boxShadow: {
        soft: "0 10px 30px -12px rgba(37, 99, 235, 0.25)",
      },
      keyframes: {
        "fade-in-up": {
          "0%": { opacity: "0", transform: "translateY(12px)" },
          "100%": { opacity: "1", transform: "translateY(0)" },
        },
      },
      animation: {
        "fade-in-up": "fade-in-up 0.5s ease-out both",
      },
    },
  },
  plugins: [],
};

export default config;
