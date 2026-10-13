import js from '@eslint/js';
import tseslint from 'typescript-eslint';
import globals from 'globals';
export default [
  {
    ignores: [
      '.build/**',
      'node_modules/**',
      'runtime/profile-spike/**',
      'runtime/profile-v1/artifacts/**',
    ],
  },
  { ...js.configs.recommended, files: ['**/*.mjs'], languageOptions: { globals: globals.node } },
  ...tseslint.configs.recommendedTypeChecked.map((config) => ({
    ...config,
    files: ['runtime/**/*.ts'],
  })),
  {
    files: ['runtime/**/*.ts'],
    languageOptions: {
      parserOptions: { project: './tsconfig.json', tsconfigRootDir: import.meta.dirname },
    },
    rules: {
      '@typescript-eslint/consistent-type-imports': 'error',
      '@typescript-eslint/no-explicit-any': 'error',
    },
  },

  {
    files: ['runtime/contracts/src/**/*.ts'],
    rules: {
      'no-restricted-imports': [
        'error',
        {
          patterns: [
            '**/authoring/**',
            '**/validation/**',
            '**/renderer/**',
            '**/artifacts/**',
            '**/profile-v1/**',
          ],
        },
      ],
    },
  },
  {
    files: ['runtime/authoring/src/**/*.ts'],
    rules: {
      'no-restricted-imports': [
        'error',
        { patterns: ['**/validation/**', '**/renderer/**', '**/artifacts/**', '**/profile-v1/**'] },
      ],
    },
  },
  {
    files: ['runtime/renderer/src/**/*.ts'],
    rules: {
      'no-restricted-imports': [
        'error',
        {
          patterns: [
            'node:*',
            '**/authoring/**',
            '**/validation/**',
            '**/artifacts/**',
            '**/profile-v1/**',
          ],
        },
      ],
    },
  },
  {
    files: ['runtime/validation/src/**/*.ts'],
    rules: {
      'no-restricted-imports': [
        'error',
        { patterns: ['**/renderer/**', '**/artifacts/**', '**/profile-v1/**'] },
      ],
    },
  },
];
