import { defineConfig, loadEnv } from "vite";
import react from "@vitejs/plugin-react";
import eslint from "vite-plugin-eslint";
import viteTsconfigPaths from "vite-tsconfig-paths";
import { fileURLToPath, URL } from "node:url";
import fs from "fs";
import path from "path";
import child_process from "child_process";

const baseFolder =
  process.env.APPDATA !== undefined && process.env.APPDATA !== ""
    ? `${process.env.APPDATA}/ASP.NET/https`
    : `${process.env.HOME}/.aspnet/https`;

const certificateArg = process.argv
  .map((arg) => arg.match(/--name=(?<value>.+)/i))
  .filter(Boolean)[0];
const certificateName = certificateArg
  ? certificateArg.groups.value
  : "reactapp3.client";

if (!certificateName) {
  console.error(
    "Invalid certificate name. Run this script in the context of an npm/yarn script or pass --name=<<app>> explicitly."
  );
  process.exit(-1);
}

const certFilePath = path.join(baseFolder, `${certificateName}.pem`);
const keyFilePath = path.join(baseFolder, `${certificateName}.key`);

if (!fs.existsSync(certFilePath) || !fs.existsSync(keyFilePath)) {
  if (
    0 !==
    child_process.spawnSync(
      "dotnet",
      [
        "dev-certs",
        "https",
        "--export-path",
        certFilePath,
        "--format",
        "Pem",
        "--no-password",
      ],
      { stdio: "inherit" }
    ).status
  ) {
    throw new Error("Could not create certificate.");
  }
}

export default defineConfig(({ mode }) => {
  // Load app-level env vars to node-level env vars.
  const env = { ...process.env, ...loadEnv(mode, process.cwd()) };
  process.env = env;

  const target = env.ASPNETCORE_HTTPS_PORT
    ? `https://localhost:${env.ASPNETCORE_HTTPS_PORT}`
    : env.ASPNETCORE_URLS
    ? env.ASPNETCORE_URLS.split(";")[0]
    : "http://localhost:7182";

  return {
    build: {
      outDir: "build",
    },
    plugins: [react(), eslint(), viteTsconfigPaths()],
    resolve: {
      alias: {
        "@": fileURLToPath(new URL("./src", import.meta.url)),
      },
    },
    server: {
      port: 5000,
      https: {
        key: fs.readFileSync(keyFilePath),
        cert: fs.readFileSync(certFilePath),
      },
      strictPort: true,
      proxy: {
        "/api": {
          target: target,
          // Handle errors to prevent the proxy middleware from crashing when
          // the ASP NET Core webserver is unavailable
          onError: (err, req, resp, target) => {
            console.error(`${err.message}`);
          },
          changeOrigin: true,
          secure: false,
          rewrite: (path) => path.replace(/^\/api/, "/api"),
          // Uncomment this line to add support for proxying websockets
          //ws: true,
          headers: {
            Connection: "Keep-Alive",
          },
        },
        "/swagger": {
          target: target,
          // Handle errors to prevent the proxy middleware from crashing when
          // the ASP NET Core webserver is unavailable
          onError: (err, req, resp, target) => {
            console.error(`${err.message}`);
          },
          changeOrigin: true,
          secure: false,
          rewrite: (path) => path.replace(/^\/swagger/, "/swagger"),
          // Uncomment this line to add support for proxying websockets
          //ws: true,
          headers: {
            Connection: "Keep-Alive",
          },
        },
      },
    },
  };
});
