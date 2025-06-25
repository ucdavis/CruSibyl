const fs = require('fs');
const path = require('path');

// Read extracted CSS classes from Htmx.Components
let extractedClasses = [];
try {
  const extractedClassesPath = path.join(__dirname, '../Htmx.Components/content/extracted-css-classes.txt');
  if (fs.existsSync(extractedClassesPath)) {
    const content = fs.readFileSync(extractedClassesPath, 'utf8');
    extractedClasses = content.split('\n').filter(line => line.trim() !== '');
  }
} catch (e) {
  console.warn('Could not read extracted CSS classes:', e.message);
}

/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./Views/**/*.cshtml",
    "./wwwroot/**/*.html",
    // Include Htmx.Components views
    "../Htmx.Components/Views/**/*.cshtml",
  ],
  safelist: extractedClasses,
  theme: {
    extend: {},
  },
  plugins: [
    require('daisyui'),
  ],
  daisyui: {
    themes: true, // false: only light + dark | true: all themes | array: specific themes like this ["light", "dark", "cupcake"]
    darkTheme: "dark", // name of one of the included themes for dark mode
    base: true, // applies background color and foreground color for root element by default
    styled: true, // include daisyUI colors and design decisions for all components
    utils: true, // adds responsive and modifier utility classes
    rtl: false, // rotate style direction from left-to-right to right-to-left. You also need to add dir="rtl" to your html tag and install `tailwindcss-flip` plugin for Tailwind CSS.
    prefix: "", // prefix for daisyUI classnames (components, modifiers and responsive class names. Not colors)
    logs: true, // Shows info about daisyUI version and used config in the console when building your CSS
  },
}
