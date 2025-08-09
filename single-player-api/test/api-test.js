const axios = require('axios');

// API 서버 테스트 스크립트
const API_BASE_URL = process.env.API_BASE_URL || 'http://localhost:8080/api';
const TEST_JWT_TOKEN = process.env.TEST_JWT_TOKEN || null;

class ApiTester {
  constructor() {
    this.baseURL = API_BASE_URL;
    this.token = TEST_JWT_TOKEN;
    this.client = axios.create({
      baseURL: this.baseURL,
      timeout: 5000,
      headers: {
        'Content-Type': 'application/json'
      }
    });

    // JWT 토큰이 있으면 기본 헤더에 추가
    if (this.token) {
      this.client.defaults.headers.common['Authorization'] = `Bearer ${this.token}`;
    }
  }

  // 테스트 결과 출력
  logTest(testName, success, data = null, error = null) {
    const status = success ? '✅' : '❌';
    console.log(`${status} ${testName}`);
    
    if (success && data) {
      console.log('   Response:', JSON.stringify(data, null, 2));
    }
    
    if (error) {
      console.log('   Error:', error.message);
      if (error.response) {
        console.log('   Status:', error.response.status);
        console.log('   Data:', error.response.data);
      }
    }
    console.log();
  }

  // 헬스체크 테스트
  async testHealthCheck() {
    try {
      const response = await this.client.get('/health');
      this.logTest('Health Check', true, response.data);
      return true;
    } catch (error) {
      this.logTest('Health Check', false, null, error);
      return false;
    }
  }

  // 라이브니스 체크 테스트
  async testLivenessCheck() {
    try {
      const response = await this.client.get('/health/live');
      this.logTest('Liveness Check', true, response.data);
      return true;
    } catch (error) {
      this.logTest('Liveness Check', false, null, error);
      return false;
    }
  }

  // 레디니스 체크 테스트
  async testReadinessCheck() {
    try {
      const response = await this.client.get('/health/ready');
      this.logTest('Readiness Check', true, response.data);
      return true;
    } catch (error) {
      this.logTest('Readiness Check', false, null, error);
      return false;
    }
  }

  // API 루트 테스트
  async testApiRoot() {
    try {
      const response = await this.client.get('/');
      this.logTest('API Root', true, response.data);
      return true;
    } catch (error) {
      this.logTest('API Root', false, null, error);
      return false;
    }
  }

  // JWT 토큰 검증 테스트 (토큰이 있을 때만)
  async testTokenValidation() {
    if (!this.token) {
      this.logTest('Token Validation', false, null, { message: 'No test token provided' });
      return false;
    }

    try {
      const response = await this.client.post('/auth/validate');
      this.logTest('Token Validation', true, response.data);
      return true;
    } catch (error) {
      this.logTest('Token Validation', false, null, error);
      return false;
    }
  }

  // 스테이지 데이터 조회 테스트 (토큰이 있을 때만)
  async testStageData() {
    if (!this.token) {
      this.logTest('Stage Data', false, null, { message: 'No test token provided' });
      return false;
    }

    try {
      const response = await this.client.get('/stages/1');
      this.logTest('Stage Data (Stage 1)', true, response.data);
      return true;
    } catch (error) {
      this.logTest('Stage Data (Stage 1)', false, null, error);
      return false;
    }
  }

  // 사용자 프로필 테스트 (토큰이 있을 때만)
  async testUserProfile() {
    if (!this.token) {
      this.logTest('User Profile', false, null, { message: 'No test token provided' });
      return false;
    }

    try {
      const response = await this.client.get('/user/profile');
      this.logTest('User Profile', true, response.data);
      return true;
    } catch (error) {
      this.logTest('User Profile', false, null, error);
      return false;
    }
  }

  // 무인증 API 테스트
  async testUnauthenticatedEndpoint() {
    try {
      // Authorization 헤더 제거
      const response = await this.client.get('/stages/1', {
        headers: { Authorization: undefined }
      });
      this.logTest('Unauthenticated Access', false, null, { message: 'Should have been rejected' });
      return false;
    } catch (error) {
      if (error.response && error.response.status === 401) {
        this.logTest('Unauthenticated Access (Expected 401)', true, { status: 401 });
        return true;
      }
      this.logTest('Unauthenticated Access', false, null, error);
      return false;
    }
  }

  // 존재하지 않는 엔드포인트 테스트
  async testNotFoundEndpoint() {
    try {
      const response = await this.client.get('/nonexistent');
      this.logTest('404 Test', false, null, { message: 'Should have returned 404' });
      return false;
    } catch (error) {
      if (error.response && error.response.status === 404) {
        this.logTest('404 Test (Expected 404)', true, { status: 404 });
        return true;
      }
      this.logTest('404 Test', false, null, error);
      return false;
    }
  }

  // 전체 테스트 실행
  async runAllTests() {
    console.log('🧪 Blokus Single Player API Test Suite');
    console.log('=====================================');
    console.log(`🌐 Testing API at: ${this.baseURL}`);
    console.log(`🔑 JWT Token: ${this.token ? 'Provided' : 'Not provided'}`);
    console.log();

    const results = [];
    
    // 기본 엔드포인트 테스트
    results.push(await this.testHealthCheck());
    results.push(await this.testLivenessCheck());
    results.push(await this.testReadinessCheck());
    results.push(await this.testApiRoot());
    results.push(await this.testNotFoundEndpoint());
    
    // 인증이 필요한 엔드포인트 테스트
    if (this.token) {
      results.push(await this.testTokenValidation());
      results.push(await this.testStageData());
      results.push(await this.testUserProfile());
    }
    
    // 무인증 접근 테스트
    results.push(await this.testUnauthenticatedEndpoint());

    // 결과 요약
    const passed = results.filter(r => r).length;
    const total = results.length;
    
    console.log('=====================================');
    console.log(`📊 Test Results: ${passed}/${total} passed`);
    
    if (passed === total) {
      console.log('🎉 All tests passed!');
    } else {
      console.log(`⚠️  ${total - passed} tests failed`);
    }

    return passed === total;
  }
}

// 테스트 실행
async function main() {
  const tester = new ApiTester();
  
  try {
    const allPassed = await tester.runAllTests();
    process.exit(allPassed ? 0 : 1);
  } catch (error) {
    console.error('❌ Test suite failed:', error.message);
    process.exit(1);
  }
}

// 스크립트가 직접 실행될 때만 테스트 실행
if (require.main === module) {
  main();
}

module.exports = ApiTester;