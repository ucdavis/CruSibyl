// This script extracts css classes and styles from .cs files in the specified directory
const fs = require("fs");
const path = require("path");

const sourceDir = path.resolve(__dirname, ".."); // adjust if your .cs files live elsewhere
const outputFile = path.join(__dirname, "tailwind.extra-classes.txt");

// We're currently only applying classes via fluent With... methods. This might need to be adjusted
// if we start using other methods or if the pattern changes.
const CLASS_PATTERN = /\.With(Class|Icon)\s*\(\s*"([^"]*)"/g;
const STYLE_PATTERN = /\.With(Style)\s*\(\s*"([^"]*)"/g;

function walk(dir, fileCallback) {
  fs.readdirSync(dir, { withFileTypes: true }).forEach((entry) => {
    const fullPath = path.join(dir, entry.name);
    if (
      entry.isDirectory() &&
      !["bin", "obj", "node_modules", ".git"].includes(entry.name)
    ) {
      walk(fullPath, fileCallback);
    } else if (entry.isFile() && entry.name.endsWith(".cs")) {
      fileCallback(fullPath);
    }
  });
}

function extractMatches(content, regex) {
  const matches = [];
  let match;
  while ((match = regex.exec(content)) !== null) {
    matches.push(...match[2].split(/\s+/)); // split class list on whitespace
  }
  return matches;
}

const allClasses = new Set();

walk(sourceDir, (file) => {
  const content = fs.readFileSync(file, "utf-8");
  extractMatches(content, CLASS_PATTERN).forEach((cls) => allClasses.add(cls));
  extractMatches(content, STYLE_PATTERN).forEach((style) =>
    allClasses.add(style)
  ); // optional
});

fs.writeFileSync(outputFile, Array.from(allClasses).join("\n"));
console.log(`Extracted ${allClasses.size} classes to ${outputFile}`);
