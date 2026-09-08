const angular = require('angular-eslint');

const withFiles = (configs, files) => configs.map((config) => ({ ...config, files }));

module.exports = [
  {
    ignores: ['dist/**', '.angular/**', 'node_modules/**', 'coverage/**'],
  },
  ...withFiles(angular.configs.tsRecommended, ['**/*.ts']),
  {
    files: ['**/*.ts'],
    processor: angular.processInlineTemplates,
    rules: {
      '@angular-eslint/prefer-inject': 'off',
    },
  },
  ...withFiles(angular.configs.templateRecommended, ['**/*.html']),
  {
    files: ['**/*.html'],
    rules: {
      '@angular-eslint/template/prefer-control-flow': 'off',
    },
  },
];
