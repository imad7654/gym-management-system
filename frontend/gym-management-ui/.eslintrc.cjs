/**
 * The config `npm run lint` has always assumed and never had. The plugins below were in
 * package.json from the start, but with no config file the command failed immediately, so
 * nothing in this project had ever been linted until 31 August 2026.
 */
module.exports = {
  root: true,
  env: { browser: true, es2020: true },
  extends: [
    'eslint:recommended',
    'plugin:@typescript-eslint/recommended',
    'plugin:react-hooks/recommended',
  ],
  ignorePatterns: ['dist', 'node_modules', '.eslintrc.cjs', 'vite.config.ts'],
  parser: '@typescript-eslint/parser',
  parserOptions: { ecmaVersion: 'latest', sourceType: 'module' },
  plugins: ['react-refresh'],
  rules: {
    'react-refresh/only-export-components': [
      'warn',
      { allowConstantExport: true },
    ],
    // The codebase uses `interface Props {}` style empty extensions in a few places and
    // MUI's sx prop pushes `any` in others. Warn rather than error so the command stays
    // usable; these are worth cleaning up, not worth blocking a build over.
    '@typescript-eslint/no-explicit-any': 'warn',
    '@typescript-eslint/no-unused-vars': [
      'error',
      { argsIgnorePattern: '^_', varsIgnorePattern: '^_' },
    ],
  },
};
