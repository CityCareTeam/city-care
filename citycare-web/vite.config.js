import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  server: {
    port: 3000,
    proxy: {
      // Redirige tous les appels /auth, /incidents, /users vers l'API
      "/auth":      { target: "http://localhost:5158", changeOrigin: true },
      "/incidents": { target: "http://localhost:5158", changeOrigin: true },
      "/users":     { target: "http://localhost:5158", changeOrigin: true },
      "/geocode":   { target: "http://localhost:5158", changeOrigin: true },
    },
  },
});
