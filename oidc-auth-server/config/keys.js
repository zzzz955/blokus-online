const { generateKeyPair, importJWK, exportJWK } = require('jose')
const fs = require('fs').promises
const path = require('path')
const logger = require('./logger')

class KeyManager {
  constructor() {
    this.privateKey = null
    this.publicKey = null
    this.kid = null
    this.keyPairJWK = null
    this.keysDir = path.join(__dirname, '..', 'keys')
  }

  async initialize() {
    try {
      // keys 디렉토리 생성
      await this.ensureKeysDirectory()

      // 기존 키 로드 시도
      const keyLoaded = await this.loadExistingKeys()
      
      if (!keyLoaded) {
        // 새로운 키 페어 생성
        await this.generateNewKeyPair()
      }

      logger.info('Key manager initialized successfully', {
        kid: this.kid,
        hasPrivateKey: !!this.privateKey,
        hasPublicKey: !!this.publicKey
      })

      return true
    } catch (error) {
      logger.error('Failed to initialize key manager', error)
      throw error
    }
  }

  async ensureKeysDirectory() {
    try {
      await fs.access(this.keysDir)
    } catch (error) {
      // 디렉토리가 없으면 생성
      await fs.mkdir(this.keysDir, { recursive: true })
      logger.info('Created keys directory', { path: this.keysDir })
    }
  }

  async loadExistingKeys() {
    try {
      const privateKeyPath = path.join(this.keysDir, 'private.jwk')
      const publicKeyPath = path.join(this.keysDir, 'public.jwk')
      const metadataPath = path.join(this.keysDir, 'metadata.json')

      // 파일 존재 확인
      await fs.access(privateKeyPath)
      await fs.access(publicKeyPath)
      await fs.access(metadataPath)

      // 키 파일들 로드
      const privateKeyJWK = JSON.parse(await fs.readFile(privateKeyPath, 'utf8'))
      const publicKeyJWK = JSON.parse(await fs.readFile(publicKeyPath, 'utf8'))
      const metadata = JSON.parse(await fs.readFile(metadataPath, 'utf8'))

      // 키 import
      this.privateKey = await importJWK(privateKeyJWK, 'RS256')
      this.publicKey = await importJWK(publicKeyJWK, 'RS256')
      this.kid = metadata.kid
      this.keyPairJWK = {
        private: privateKeyJWK,
        public: publicKeyJWK
      }

      logger.info('Loaded existing key pair', {
        kid: this.kid,
        createdAt: metadata.createdAt
      })

      return true
    } catch (error) {
      logger.info('No existing keys found, will generate new ones', {
        error: error.message
      })
      return false
    }
  }

  async generateNewKeyPair() {
    try {
      logger.info('Generating new RSA key pair...')

      // RSA 키 페어 생성 (2048 bits)
      const { privateKey, publicKey } = await generateKeyPair('RS256', {
        modulusLength: 2048
      })

      // JWK 형태로 export
      const privateKeyJWK = await exportJWK(privateKey)
      const publicKeyJWK = await exportJWK(publicKey)

      // Key ID 생성 (타임스탬프 기반)
      const kid = `oidc-key-${Date.now()}`

      // JWK에 kid 추가
      privateKeyJWK.kid = kid
      privateKeyJWK.use = 'sig'
      privateKeyJWK.alg = 'RS256'

      publicKeyJWK.kid = kid
      publicKeyJWK.use = 'sig'
      publicKeyJWK.alg = 'RS256'

      // 키 저장
      const privateKeyPath = path.join(this.keysDir, 'private.jwk')
      const publicKeyPath = path.join(this.keysDir, 'public.jwk')
      const metadataPath = path.join(this.keysDir, 'metadata.json')

      await fs.writeFile(privateKeyPath, JSON.stringify(privateKeyJWK, null, 2))
      await fs.writeFile(publicKeyPath, JSON.stringify(publicKeyJWK, null, 2))
      await fs.writeFile(metadataPath, JSON.stringify({
        kid,
        algorithm: 'RS256',
        use: 'sig',
        createdAt: new Date().toISOString(),
        version: '1.0'
      }, null, 2))

      // 인스턴스 변수 설정
      this.privateKey = privateKey
      this.publicKey = publicKey
      this.kid = kid
      this.keyPairJWK = {
        private: privateKeyJWK,
        public: publicKeyJWK
      }

      logger.info('Generated and saved new key pair', {
        kid,
        modulusLength: 2048,
        algorithm: 'RS256'
      })

      return true
    } catch (error) {
      logger.error('Failed to generate new key pair', error)
      throw error
    }
  }

  // JWKS용 공개 키 반환
  getJWKS() {
    if (!this.keyPairJWK) {
      throw new Error('Keys not initialized')
    }

    return {
      keys: [this.keyPairJWK.public]
    }
  }

  // JWT 서명용 private key 반환
  getPrivateKey() {
    if (!this.privateKey) {
      throw new Error('Private key not initialized')
    }
    return this.privateKey
  }

  // JWT 검증용 public key 반환
  getPublicKey() {
    if (!this.publicKey) {
      throw new Error('Public key not initialized')
    }
    return this.publicKey
  }

  // Key ID 반환
  getKid() {
    if (!this.kid) {
      throw new Error('Key ID not initialized')
    }
    return this.kid
  }

  // 키 회전 (새로운 키 페어 생성 및 기존 키 백업)
  async rotateKeys() {
    try {
      logger.info('Starting key rotation...')

      // 기존 키 백업
      const backupDir = path.join(this.keysDir, 'backup', new Date().toISOString().split('T')[0])
      await fs.mkdir(backupDir, { recursive: true })

      if (this.keyPairJWK) {
        await fs.writeFile(
          path.join(backupDir, 'private.jwk'),
          JSON.stringify(this.keyPairJWK.private, null, 2)
        )
        await fs.writeFile(
          path.join(backupDir, 'public.jwk'),
          JSON.stringify(this.keyPairJWK.public, null, 2)
        )
        await fs.writeFile(
          path.join(backupDir, 'metadata.json'),
          JSON.stringify({
            kid: this.kid,
            rotatedAt: new Date().toISOString(),
            reason: 'key_rotation'
          }, null, 2)
        )

        logger.info('Backed up existing keys', { backupDir })
      }

      // 새로운 키 페어 생성
      await this.generateNewKeyPair()

      logger.info('Key rotation completed successfully', {
        newKid: this.kid,
        backupLocation: backupDir
      })

      return true
    } catch (error) {
      logger.error('Key rotation failed', error)
      throw error
    }
  }

  // 키 건강성 체크
  async healthCheck() {
    try {
      if (!this.privateKey || !this.publicKey || !this.kid) {
        return { healthy: false, reason: 'keys_not_initialized' }
      }

      // 간단한 서명/검증 테스트
      const testPayload = { test: true, iat: Math.floor(Date.now() / 1000) }
      const { SignJWT, jwtVerify } = require('jose')

      const jwt = await new SignJWT(testPayload)
        .setProtectedHeader({ alg: 'RS256', kid: this.kid })
        .setIssuedAt()
        .setExpirationTime('1m')
        .sign(this.privateKey)

      const { payload } = await jwtVerify(jwt, this.publicKey)

      if (payload.test === true) {
        return { healthy: true, kid: this.kid }
      } else {
        return { healthy: false, reason: 'verification_failed' }
      }
    } catch (error) {
      logger.error('Key health check failed', error)
      return { healthy: false, reason: error.message }
    }
  }
}

// 싱글톤 인스턴스
const keyManager = new KeyManager()

module.exports = keyManager