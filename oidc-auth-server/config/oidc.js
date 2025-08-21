const logger = require('./logger')

class OIDCConfig {
  constructor() {
    this.issuer = process.env.OIDC_ISSUER || 'http://localhost:9000'
    this.baseUrl = process.env.OIDC_BASE_URL || 'http://localhost:9000'
    
    // 토큰 수명 (슬라이딩 윈도우: 30일 + 최대 90일)
    this.tokenLifetimes = {
      accessToken: process.env.ACCESS_TOKEN_LIFETIME || '10m',  // 10 minutes
      refreshToken: process.env.REFRESH_TOKEN_LIFETIME || '30d', // 30 days (sliding window)
      refreshTokenMaxLifetime: process.env.REFRESH_TOKEN_MAX_LIFETIME || '90d', // 90 days maximum
      authorizationCode: process.env.AUTH_CODE_LIFETIME || '10m' // 10 minutes
    }

    // 지원하는 클라이언트들
    this.clients = {
      'qt-desktop-client': {
        client_id: 'qt-desktop-client',
        client_secret: null, // Public client (PKCE 사용)
        redirect_uris: [
          'http://localhost:8080/auth/callback',
          'http://127.0.0.1:8080/auth/callback'
        ],
        grant_types: ['authorization_code', 'refresh_token'],
        response_types: ['code'],
        token_endpoint_auth_method: 'none', // PKCE만 사용
        require_pkce: true,
        client_type: 'public'
      },
      'unity-mobile-client': {
        client_id: 'unity-mobile-client',
        client_secret: null, // Public client (PKCE 사용)
        redirect_uris: [
          'blokus://auth/callback',
          'http://localhost:7777/auth/callback' // 로컬 테스트용
        ],
        grant_types: ['authorization_code', 'refresh_token'],
        response_types: ['code'],
        token_endpoint_auth_method: 'none', // PKCE만 사용
        require_pkce: true,
        client_type: 'public'
      },
      'nextjs-web-client': {
        client_id: 'nextjs-web-client',
        client_secret: process.env.WEB_CLIENT_SECRET || 'web-client-secret-change-in-production',
        redirect_uris: [
          'http://localhost:3000/api/auth/callback/blokus-oidc',
          'https://blokus-online.mooo.com/api/auth/callback/blokus-oidc'
        ],
        grant_types: ['authorization_code', 'refresh_token'],
        response_types: ['code'],
        token_endpoint_auth_method: 'client_secret_post',
        require_pkce: false, // BFF에서는 PKCE 선택사항
        client_type: 'confidential'
      }
    }
  }

  // OIDC Discovery 메타데이터 생성
  getDiscoveryDocument() {
    return {
      issuer: this.issuer,
      authorization_endpoint: `${this.baseUrl}/authorize`,
      token_endpoint: `${this.baseUrl}/token`,
      jwks_uri: `${this.baseUrl}/jwks.json`,
      introspection_endpoint: `${this.baseUrl}/introspect`,
      revocation_endpoint: `${this.baseUrl}/revocation`,
      
      // 지원하는 response types
      response_types_supported: ['code'],
      
      // 지원하는 grant types
      grant_types_supported: ['authorization_code', 'refresh_token'],
      
      // 지원하는 인증 방법
      token_endpoint_auth_methods_supported: [
        'client_secret_post',
        'client_secret_basic',
        'none' // PKCE for public clients
      ],
      
      // 지원하는 서명 알고리즘
      id_token_signing_alg_values_supported: ['RS256'],
      
      // 지원하는 scopes
      scopes_supported: ['openid', 'profile', 'email'],
      
      // 지원하는 claims
      claims_supported: [
        'sub',
        'iss',
        'aud',
        'exp',
        'iat',
        'auth_time',
        'nonce',
        'email',
        'email_verified',
        'name',
        'preferred_username'
      ],
      
      // PKCE 지원
      code_challenge_methods_supported: ['S256'],
      
      // 기타 기능들
      request_parameter_supported: false,
      request_uri_parameter_supported: false,
      require_request_uri_registration: false,
      claims_parameter_supported: false,
      
      // 토큰 교환
      introspection_endpoint_auth_methods_supported: [
        'client_secret_post',
        'client_secret_basic'
      ],
      
      revocation_endpoint_auth_methods_supported: [
        'client_secret_post',
        'client_secret_basic',
        'none'
      ]
    }
  }

  // 클라이언트 정보 조회
  getClient(clientId) {
    return this.clients[clientId] || null
  }

  // 클라이언트 검증
  validateClient(clientId, clientSecret = null) {
    const client = this.getClient(clientId)
    if (!client) {
      return { valid: false, error: 'invalid_client' }
    }

    // Public client (PKCE 사용)
    if (client.client_type === 'public') {
      if (clientSecret) {
        return { valid: false, error: 'invalid_client_auth' }
      }
      return { valid: true, client }
    }

    // Confidential client (client secret 필요)
    if (client.client_type === 'confidential') {
      if (!clientSecret || clientSecret !== client.client_secret) {
        return { valid: false, error: 'invalid_client' }
      }
      return { valid: true, client }
    }

    return { valid: false, error: 'invalid_client_type' }
  }

  // Redirect URI 검증
  validateRedirectUri(clientId, redirectUri) {
    const client = this.getClient(clientId)
    if (!client) {
      return false
    }

    return client.redirect_uris.includes(redirectUri)
  }

  // Scope 검증
  validateScope(scope) {
    const supportedScopes = ['openid', 'profile', 'email']
    const requestedScopes = scope ? scope.split(' ') : []

    // openid는 필수
    if (!requestedScopes.includes('openid')) {
      return { valid: false, error: 'invalid_scope', description: 'openid scope is required' }
    }

    // 지원하지 않는 scope 확인
    const unsupportedScopes = requestedScopes.filter(s => !supportedScopes.includes(s))
    if (unsupportedScopes.length > 0) {
      return { 
        valid: false, 
        error: 'invalid_scope', 
        description: `Unsupported scopes: ${unsupportedScopes.join(', ')}` 
      }
    }

    return { valid: true, scopes: requestedScopes }
  }

  // 토큰 수명을 초단위로 변환
  getTokenLifetimeInSeconds(tokenType) {
    const lifetime = this.tokenLifetimes[tokenType]
    if (!lifetime) {
      throw new Error(`Unknown token type: ${tokenType}`)
    }

    // 문자열 파싱 (예: '10m', '14d', '3600s')
    const unit = lifetime.slice(-1)
    const value = parseInt(lifetime.slice(0, -1))

    switch (unit) {
      case 's': return value
      case 'm': return value * 60
      case 'h': return value * 60 * 60
      case 'd': return value * 24 * 60 * 60
      default: 
        throw new Error(`Invalid token lifetime format: ${lifetime}`)
    }
  }

  // 설정 검증
  validate() {
    const errors = []

    if (!this.issuer) {
      errors.push('OIDC_ISSUER is required')
    }

    if (!this.baseUrl) {
      errors.push('OIDC_BASE_URL is required')
    }

    // 각 클라이언트 검증
    Object.entries(this.clients).forEach(([clientId, client]) => {
      if (!client.redirect_uris || client.redirect_uris.length === 0) {
        errors.push(`Client ${clientId} must have at least one redirect URI`)
      }

      if (client.client_type === 'confidential' && !client.client_secret) {
        errors.push(`Confidential client ${clientId} must have a client secret`)
      }
    })

    if (errors.length > 0) {
      logger.error('OIDC configuration validation failed', { errors })
      throw new Error(`OIDC configuration errors: ${errors.join(', ')}`)
    }

    logger.info('OIDC configuration validated successfully', {
      issuer: this.issuer,
      clientCount: Object.keys(this.clients).length,
      tokenLifetimes: this.tokenLifetimes
    })

    return true
  }

  // 환경별 설정 오버라이드
  applyEnvironmentOverrides() {
    if (process.env.NODE_ENV === 'production') {
      // 프로덕션 환경 설정
      this.issuer = process.env.OIDC_ISSUER || 'https://blokus-online.mooo.com'
      this.baseUrl = process.env.OIDC_BASE_URL || 'https://blokus-online.mooo.com'

      // 프로덕션에서는 localhost redirect URI 제거
      Object.values(this.clients).forEach(client => {
        client.redirect_uris = client.redirect_uris.filter(uri => 
          !uri.includes('localhost') && !uri.includes('127.0.0.1')
        )
      })
    }

    logger.info('Applied environment-specific OIDC configuration', {
      environment: process.env.NODE_ENV || 'development',
      issuer: this.issuer
    })
  }
}

// 싱글톤 인스턴스
const oidcConfig = new OIDCConfig()

// 환경별 설정 적용 및 검증
oidcConfig.applyEnvironmentOverrides()
oidcConfig.validate()

module.exports = oidcConfig