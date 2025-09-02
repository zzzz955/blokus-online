const logger = require('./logger')
const { env } = require('./env')

class OIDCConfig {
  constructor() {
    this.issuer = env.OIDC_ISSUER
    this.baseUrl = env.OIDC_BASE_URL
    
    // 토큰 수명 (슬라이딩 윈도우: 30일 + 최대 90일)
    this.tokenLifetimes = {
      accessToken: env.ACCESS_TOKEN_LIFETIME,  // 10 minutes
      refreshToken: env.REFRESH_TOKEN_LIFETIME, // 30 days (sliding window)
      refreshTokenMaxLifetime: env.REFRESH_TOKEN_MAX_LIFETIME, // 90 days maximum
      authorizationCode: env.AUTH_CODE_LIFETIME // 10 minutes
    }

    // 지원하는 클라이언트들
    this.clients = {
      'blokus-desktop-client': {
        client_id: 'blokus-desktop-client',
        client_secret: null, // Public client (PKCE 사용)
        redirect_uris: [
          'qt-desktop-client://auth/callback'
          // 동적 localhost 포트는 validateRedirectUri에서 패턴 매칭으로 처리
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
          'http://localhost:7777/auth/callback', // Unity 에디터 localhost 방식
          'http://127.0.0.1:7777/auth/callback',  // Unity 에디터 대안
          'https://blokus-online.mooo.com/oidc/unity-editor-callback', // Unity 에디터 배포 서버 방식 (레거시)
          'https://blokus-online.mooo.com/oidc/auth/google/callback' // 새로운 서버 콜백 방식
        ],
        grant_types: ['authorization_code', 'refresh_token'],
        response_types: ['code'],
        token_endpoint_auth_method: 'none', // PKCE만 사용
        require_pkce: true,
        client_type: 'public'
      },
      'nextjs-web-client': {
        client_id: 'nextjs-web-client',
        client_secret: env.WEB_CLIENT_SECRET,
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

    // Qt 클라이언트는 동적 localhost 포트 허용
    if (clientId === 'blokus-desktop-client') {
      // localhost:임의포트/callback 패턴 허용
      const localhostPattern = /^http:\/\/(localhost|127\.0\.0\.1):\d+\/callback$/
      if (localhostPattern.test(redirectUri)) {
        return true
      }
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

}

// 싱글톤 인스턴스
const oidcConfig = new OIDCConfig()

// 설정 검증
oidcConfig.validate()

module.exports = oidcConfig