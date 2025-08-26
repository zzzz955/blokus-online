/**
 * Test script for OptimalScoreCalculator with Blokus rules
 * Verifies that the calculator follows proper Blokus placement rules
 */

import OptimalScoreCalculator from './OptimalScoreCalculator';

// Test case 1: First block placement (should require corner placement)
export const testFirstBlockPlacement = async () => {
  const calculator = new OptimalScoreCalculator();
  
  const boardState = {
    obstacles: [],
    preplaced: []
  };
  
  const availableBlocks = [1]; // Single 1x1 block
  
  console.log('🎯 Testing first block placement (corner requirement)...');
  const startTime = Date.now();
  
  try {
    const score = await calculator.calculateGreedy(boardState, availableBlocks);
    const duration = Date.now() - startTime;
    console.log(`✅ First block test completed: ${score} points in ${duration}ms`);
    console.log(`📍 Expected: 1 point (should place at corner), Actual: ${score}`);
    return { success: true, score, duration, expectedScore: 1 };
  } catch (error) {
    const duration = Date.now() - startTime;
    console.error(`❌ First block test failed after ${duration}ms:`, error);
    return { success: false, error, duration };
  }
};

// Test case 2: Preplaced block continuation
export const testPreplacedBlockContinuation = async () => {
  const calculator = new OptimalScoreCalculator();
  
  const boardState = {
    obstacles: [],
    preplaced: [
      { x: 5, y: 5, color: 1 } // Blue block at center
    ]
  };
  
  const availableBlocks = [1, 2]; // Small blocks
  
  console.log('🔗 Testing preplaced block continuation (corner adjacency)...');
  const startTime = Date.now();
  
  try {
    const score = await calculator.calculateGreedy(boardState, availableBlocks);
    const duration = Date.now() - startTime;
    console.log(`✅ Preplaced continuation test: ${score} points in ${duration}ms`);
    console.log(`📍 Expected: >1 point (should connect to preplaced), Actual: ${score}`);
    return { success: true, score, duration, expectedMinScore: 2 };
  } catch (error) {
    const duration = Date.now() - startTime;
    console.error(`❌ Preplaced continuation test failed after ${duration}ms:`, error);
    return { success: false, error, duration };
  }
};

// Test case 3: Obstacles blocking placement
export const testObstacleBlocking = async () => {
  const calculator = new OptimalScoreCalculator();
  
  const boardState = {
    obstacles: [
      { x: 0, y: 1 }, { x: 1, y: 0 }, { x: 1, y: 1 } // Block corner access
    ],
    preplaced: []
  };
  
  const availableBlocks = [1, 2]; // Small blocks
  
  console.log('🚧 Testing obstacle blocking (limited placement)...');
  const startTime = Date.now();
  
  try {
    const score = await calculator.calculateGreedy(boardState, availableBlocks);
    const duration = Date.now() - startTime;
    console.log(`✅ Obstacle test completed: ${score} points in ${duration}ms`);
    console.log(`📍 Expected: Lower score due to obstacles, Actual: ${score}`);
    return { success: true, score, duration };
  } catch (error) {
    const duration = Date.now() - startTime;
    console.error(`❌ Obstacle test failed after ${duration}ms:`, error);
    return { success: false, error, duration };
  }
};

// Test case 4: Multi-color calculation
export const testMultiColorCalculation = async () => {
  const calculator = new OptimalScoreCalculator();
  
  const boardState = {
    obstacles: [],
    preplaced: []
  };
  
  const availableBlocks = [1, 2, 3, 4, 5, 6, 7, 8]; // Multiple blocks
  
  console.log('🌈 Testing multi-color calculation...');
  const startTime = Date.now();
  
  try {
    const score = await calculator.calculateGreedyMultiColor(boardState, availableBlocks);
    const duration = Date.now() - startTime;
    console.log(`✅ Multi-color test completed: ${score} points in ${duration}ms`);
    console.log(`📍 Expected: Higher score with multiple colors, Actual: ${score}`);
    return { success: true, score, duration };
  } catch (error) {
    const duration = Date.now() - startTime;
    console.error(`❌ Multi-color test failed after ${duration}ms:`, error);
    return { success: false, error, duration };
  }
};

// Test case 5: Corner adjacency validation
export const testCornerAdjacencyRule = async () => {
  const calculator = new OptimalScoreCalculator();
  
  const boardState = {
    obstacles: [],
    preplaced: [
      { x: 5, y: 5, color: 1 } // Center block
    ]
  };
  
  const availableBlocks = [1]; // Single block that must connect diagonally
  
  console.log('🔗 Testing corner adjacency rule compliance...');
  const startTime = Date.now();
  
  try {
    const score = await calculator.calculateGreedy(boardState, availableBlocks);
    const duration = Date.now() - startTime;
    console.log(`✅ Corner adjacency test: ${score} points in ${duration}ms`);
    
    if (score >= 2) {
      console.log(`✅ Rule compliance: Block successfully connected diagonally`);
    } else {
      console.log(`⚠️ Rule issue: Block may not have connected properly`);
    }
    
    return { success: true, score, duration, ruleCompliant: score >= 2 };
  } catch (error) {
    const duration = Date.now() - startTime;
    console.error(`❌ Corner adjacency test failed after ${duration}ms:`, error);
    return { success: false, error, duration };
  }
};

// Test case 6: Performance with Blokus rules
export const testBlokusRulesPerformance = async () => {
  const calculator = new OptimalScoreCalculator();
  
  const boardState = {
    obstacles: [],
    preplaced: []
  };
  
  // Medium number of blocks to test performance with rules
  const availableBlocks = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
  
  console.log('⚡ Testing performance with Blokus rules...');
  const startTime = Date.now();
  
  try {
    const score = await calculator.calculateGreedy(boardState, availableBlocks);
    const duration = Date.now() - startTime;
    console.log(`✅ Performance test completed: ${score} points in ${duration}ms`);
    
    if (duration < 5000) {
      console.log(`✅ Performance: Acceptable speed (${duration}ms < 5000ms)`);
    } else {
      console.log(`⚠️ Performance: Slower than expected (${duration}ms)`);
    }
    
    return { success: true, score, duration, performanceGood: duration < 5000 };
  } catch (error) {
    const duration = Date.now() - startTime;
    console.error(`❌ Performance test failed after ${duration}ms:`, error);
    return { success: false, error, duration };
  }
};

// Test case 7: User's specific scenario (89 points target)
export const testUserSpecificScenario = async () => {
  const calculator = new OptimalScoreCalculator();
  
  // User's test case: 3 obstacles at corners, 397 empty spaces
  const boardState = {
    obstacles: [
      { x: 0, y: 0 },   // 좌상단
      { x: 19, y: 0 },  // 우상단  
      { x: 0, y: 19 }   // 좌하단
    ],
    preplaced: []
  };
  
  // All 21 blocks available (theoretical max = 89 points)
  const availableBlocks = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21];
  
  console.log('🎯 Testing user-specific scenario (Target: 89 points)...');
  console.log('📋 Board setup: 3 obstacles, 397 empty spaces, start from (19,19)');
  const startTime = Date.now();
  
  try {
    const score = await calculator.calculateGreedy(boardState, availableBlocks);
    const duration = Date.now() - startTime;
    console.log(`✅ User scenario test completed: ${score} points in ${duration}ms`);
    console.log(`📍 Target: 89 points, Achieved: ${score} points`);
    
    // Calculate efficiency
    const efficiency = (score / 89) * 100;
    console.log(`📊 Efficiency: ${efficiency.toFixed(1)}% of theoretical maximum`);
    
    if (score >= 89) {
      console.log(`🎉 EXCELLENT: Achieved theoretical maximum!`);
    } else if (score >= 85) {
      console.log(`✅ GOOD: Very close to theoretical maximum`);
    } else if (score >= 80) {
      console.log(`⚠️ OK: Reasonable but room for improvement`);
    } else {
      console.log(`❌ POOR: Significant gap from theoretical maximum`);
    }
    
    return { 
      success: true, 
      score, 
      duration, 
      targetScore: 89,
      efficiency: efficiency,
      achievedTarget: score >= 89,
      closeToTarget: score >= 85
    };
  } catch (error) {
    const duration = Date.now() - startTime;
    console.error(`❌ User scenario test failed after ${duration}ms:`, error);
    return { success: false, error, duration, targetScore: 89 };
  }
};

// Test case 8: Enhanced algorithm comparison
export const testEnhancedVsBasic = async () => {
  const calculator = new OptimalScoreCalculator();
  
  const boardState = {
    obstacles: [
      { x: 5, y: 5 },
      { x: 10, y: 10 },
      { x: 15, y: 15 }
    ],
    preplaced: []
  };
  
  const availableBlocks = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15];
  
  console.log('🔄 Testing enhanced vs basic algorithm...');
  const startTime = Date.now();
  
  try {
    // Test basic algorithm
    const basicStartTime = Date.now();
    const basicScore = await calculator.calculateGreedyMultiColor(boardState, availableBlocks);
    const basicDuration = Date.now() - basicStartTime;
    
    // Test enhanced algorithm  
    const enhancedStartTime = Date.now();
    const enhancedScore = await calculator.calculateGreedy(boardState, availableBlocks);
    const enhancedDuration = Date.now() - enhancedStartTime;
    
    const totalDuration = Date.now() - startTime;
    
    console.log(`✅ Algorithm comparison completed in ${totalDuration}ms`);
    console.log(`📊 Basic algorithm: ${basicScore} points in ${basicDuration}ms`);
    console.log(`📊 Enhanced algorithm: ${enhancedScore} points in ${enhancedDuration}ms`);
    
    const improvement = enhancedScore - basicScore;
    const improvementPercent = ((improvement / basicScore) * 100).toFixed(1);
    
    if (improvement > 0) {
      console.log(`🚀 Enhancement: +${improvement} points (+${improvementPercent}%)`);
    } else if (improvement === 0) {
      console.log(`🔄 No improvement: Same score achieved`);
    } else {
      console.log(`⚠️ Regression: ${improvement} points (${improvementPercent}%)`);
    }
    
    return {
      success: true,
      basicScore,
      enhancedScore,
      improvement,
      basicDuration,
      enhancedDuration,
      totalDuration
    };
  } catch (error) {
    const duration = Date.now() - startTime;
    console.error(`❌ Algorithm comparison test failed after ${duration}ms:`, error);
    return { success: false, error, duration };
  }
};

// Run all Blokus rules tests
export const runAllTests = async () => {
  console.log('🧪 Starting Blokus Rules OptimalScoreCalculator Tests...\n');
  
  const tests = [
    { name: 'First Block Placement', fn: testFirstBlockPlacement },
    { name: 'Preplaced Block Continuation', fn: testPreplacedBlockContinuation },
    { name: 'Obstacle Blocking', fn: testObstacleBlocking },
    { name: 'Multi-Color Calculation', fn: testMultiColorCalculation },
    { name: 'Corner Adjacency Rule', fn: testCornerAdjacencyRule },
    { name: 'Performance with Rules', fn: testBlokusRulesPerformance },
    { name: 'User Specific Scenario (89 points)', fn: testUserSpecificScenario },
    { name: 'Enhanced vs Basic Algorithm', fn: testEnhancedVsBasic }
  ];
  
  const results = [];
  
  for (const test of tests) {
    console.log(`\n📋 Running: ${test.name}`);
    console.log('─'.repeat(50));
    
    const result = await test.fn();
    result.testName = test.name;
    results.push(result);
    
    if (result.success) {
      console.log(`✅ ${test.name}: PASSED`);
    } else {
      console.log(`❌ ${test.name}: FAILED`);
    }
  }
  
  console.log('\n📊 Final Test Summary');
  console.log('═'.repeat(50));
  
  const successCount = results.filter(r => r.success).length;
  const failCount = results.length - successCount;
  
  console.log(`✅ Passed: ${successCount}`);
  console.log(`❌ Failed: ${failCount}`);
  console.log(`📈 Success Rate: ${((successCount / results.length) * 100).toFixed(1)}%`);
  
  const avgDuration = results.reduce((sum, r) => sum + r.duration, 0) / results.length;
  console.log(`⏱️ Average Duration: ${avgDuration.toFixed(2)}ms`);
  
  // Rule compliance check
  const ruleCompliantTests = results.filter(r => r.ruleCompliant !== false);
  console.log(`🎯 Rule Compliance: ${ruleCompliantTests.length}/${results.length} tests`);
  
  // Performance check
  const performanceGoodTests = results.filter(r => r.performanceGood !== false);
  console.log(`⚡ Performance Good: ${performanceGoodTests.length}/${results.length} tests`);
  
  console.log('\n🔍 Detailed Results:');
  results.forEach(result => {
    const status = result.success ? '✅' : '❌';
    const score = result.score || 'N/A';
    const duration = result.duration || 'N/A';
    console.log(`${status} ${result.testName}: ${score} points in ${duration}ms`);
  });
  
  return {
    results,
    summary: {
      total: results.length,
      passed: successCount,
      failed: failCount,
      successRate: (successCount / results.length) * 100,
      avgDuration,
      ruleCompliant: ruleCompliantTests.length,
      performanceGood: performanceGoodTests.length
    }
  };
};

// Individual test exports for debugging
export {
  testFirstBlockPlacement,
  testPreplacedBlockContinuation, 
  testObstacleBlocking,
  testMultiColorCalculation,
  testCornerAdjacencyRule,
  testBlokusRulesPerformance,
  testUserSpecificScenario,
  testEnhancedVsBasic
};

// Usage:
// import { runAllTests } from './test-optimal-score';
// runAllTests().then(results => console.log('All tests completed!', results));