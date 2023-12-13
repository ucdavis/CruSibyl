import "bootstrap/dist/css/bootstrap.css";
import React from "react";
import { createRoot } from "react-dom/client";
import { BrowserRouter } from "react-router-dom";
import App from "./App";

const baseUrl = document
  .getElementsByTagName("base")[0]
  .getAttribute("href") as string;
const rootElement = document.getElementById("root") as Element;
const root = createRoot(rootElement);

root.render(
  <BrowserRouter basename={baseUrl}>
    <App />
  </BrowserRouter>
);
