import js from '@eslint/js'
import globals from 'globals'
import reactHooks from 'eslint-plugin-react-hooks'
import reactRefresh from 'eslint-plugin-react-refresh'
import tseslint from 'typescript-eslint'
import { defineConfig, globalIgnores } from 'eslint/config'

/** Reglas React Compiler (eslint-plugin-react-hooks v7): off en baseline v1.0 — migración incremental. */
const reactCompilerRulesOff = {
  'react-hooks/set-state-in-effect': 'off',
  'react-hooks/refs': 'off',
  'react-hooks/immutability': 'off',
  'react-hooks/purity': 'off',
  'react-hooks/static-components': 'off',
  'react-hooks/preserve-manual-memoization': 'off',
  'react-hooks/incompatible-library': 'off',
}

/** Prohibición de acceso directo al store interno de mensajes — usar `message` de `lib/messages`. */
const noDirectMessageStore = {
  'no-restricted-imports': [
    'error',
    {
      patterns: [
        {
          group: ['**/messages/_internal/*', '**/stores/toastStore*'],
          message:
            'No importes el store interno de mensajes. Usa `import { message } from "lib/messages"`. Ver AI-RULES/VISUAL-MESSAGES.md',
        },
      ],
    },
  ],
}

/** Prohibición de input type="number" — usar ZhDecimalInput o ZhNumberInput. */
const noNativeNumberInput = {
  'no-restricted-syntax': [
    'warn',
    {
      selector: 'JSXAttribute[name.name="type"][value.value="number"]',
      message:
        'No uses <input type="number">. Usa <ZhDecimalInput> para decimales o <ZhNumberInput> para enteros. Ver components/zh/inputs/.',
    },
  ],
}

/** Prohibición de style={{}} en pantallas (governance visual). */
const inlineStylePageRules = {
  'no-restricted-syntax': [
    'error',
    {
      selector: 'JSXAttribute[name.name="style"]',
      message:
        'No uses style={{...}} en pantallas. Usa pg-* / prefijo de dominio. Ver docs/frontend-layout-conventions.md',
    },
  ],
}

export default defineConfig([
  globalIgnores(['dist', 'playwright-report', 'test-results']),
  {
    files: ['e2e/**/*.ts'],
    languageOptions: {
      globals: globals.node,
      parser: tseslint.parser,
      parserOptions: { ecmaVersion: 'latest', sourceType: 'module' },
    },
    plugins: { '@typescript-eslint': tseslint.plugin },
    extends: [tseslint.configs.recommended],
  },
  {
    files: ['**/*.{ts,tsx}'],
    ignores: ['e2e/**'],
    extends: [
      js.configs.recommended,
      tseslint.configs.recommended,
      reactHooks.configs.flat.recommended,
      reactRefresh.configs.vite,
    ],
    languageOptions: {
      globals: globals.browser,
    },
    rules: {
      ...reactCompilerRulesOff,
      'react-refresh/only-export-components': [
        'warn',
        { allowConstantExport: true },
      ],
      '@typescript-eslint/no-unused-vars': [
        'error',
        { argsIgnorePattern: '^_', varsIgnorePattern: '^_' },
      ],
    },
  },
  // Acceso directo al store de mensajes prohibido en módulos.
  {
    files: ['src/modules/**/*.{ts,tsx}'],
    rules: noDirectMessageStore,
  },
  // Estilos inline: wrappers legacy en src/pages/ y módulos de producto.
  {
    files: ['src/pages/**/*.{ts,tsx}'],
    rules: inlineStylePageRules,
  },
  {
    files: ['src/modules/**/pages/**/*.{ts,tsx}'],
    rules: {
      ...inlineStylePageRules,
      'max-lines': [
        'warn',
        { max: 400, skipBlankLines: true, skipComments: true },
      ],
    },
  },
  // Estilos inline: componentes de módulo (consumidores del Design System).
  {
    files: ['src/modules/**/components/**/*.{ts,tsx}'],
    rules: inlineStylePageRules,
  },
  // Excepciones documentadas: auth (clase C, zh-auth-*).
  {
    files: ['src/modules/auth/pages/**/*.{ts,tsx}', 'src/modules/auth/components/**/*.{ts,tsx}'],
    rules: {
      'no-restricted-syntax': 'off',
    },
  },
  // Plantillas y layouts: prohibir style inline (referencia para páginas nuevas).
  {
    files: ['src/templates/**/*.{ts,tsx}', 'src/components/layout/**/*.{ts,tsx}'],
    rules: {
      'no-restricted-syntax': [
        'error',
        {
          selector: 'JSXAttribute[name.name="style"]',
          message:
            'No uses style inline en plantillas/layout. Ver docs/frontend-layout-conventions.md',
        },
      ],
    },
  },
])
