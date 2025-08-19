module.exports = {
  env: {
    node: true,
    es2021: true,
    jest: true
  },
  extends: [
    'standard'
  ],
  parserOptions: {
    ecmaVersion: 'latest',
    sourceType: 'module'
  },
  rules: {
    // Node.js 환경에 맞는 규칙들
    'no-console': 'off',
    'no-process-exit': 'off',
    'prefer-const': 'error',
    'no-var': 'error',
    // API 파라미터에서 snake_case 허용
    camelcase: 'off'
  }
}
