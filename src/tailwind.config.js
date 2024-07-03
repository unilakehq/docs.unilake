module.exports = {
  theme: {
    extend: {
      fontFamily: {
        inter: ['Inter', 'sans-serif'],
        fira: ['"Fira Code"', 'monospace'],
      },
      colors: {
        brand: '#FF6849',
      },
    },
  },
  content: [
    './**/*.{html,js,mjs,md,cshtml,razor,cs}',
    './Pages/**/*.{cshtml,razor}',
  ],
  darkMode: 'class',
  plugins: [],
}
