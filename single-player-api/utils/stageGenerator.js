/**
 * 프로시저럴 스테이지 생성 시스템
 * 시드 기반으로 일관되고 점진적으로 어려워지는 1000+ 스테이지 자동 생성
 */
const logger = require('../config/logger')

class StageGenerator {
  constructor () {
    // 블로쿠스 블록 타입들 (실제 블록 모양에 따라 조정 필요)
    this.blockTypes = {
      // 1칸 블록
      1: { size: 1, shapes: [[1]] },

      // 2칸 블록
      2: { size: 2, shapes: [[1, 1]] },

      // 3칸 블록 (L자, 일자)
      3: {
        size: 3,
        shapes: [
          [[1, 1, 1]], // 일자
          [[1, 1], [1, 0]] // L자
        ]
      },

      // 4칸 블록 (정사각형, T자, Z자, L자)
      4: {
        size: 4,
        shapes: [
          [[1, 1], [1, 1]], // 정사각형
          [[0, 1, 0], [1, 1, 1]], // T자
          [[1, 1, 0], [0, 1, 1]], // Z자
          [[1, 0, 0], [1, 1, 1]] // L자
        ]
      },

      // 5칸 블록 (다양한 모양들)
      5: {
        size: 5,
        shapes: [
          [[1, 1, 1, 1, 1]], // 일자
          [[0, 0, 1], [1, 1, 1], [0, 1, 0]], // 십자
          [[1, 1, 1, 1], [1, 0, 0, 0]], // L자
          [[1, 1, 1], [0, 1, 1]] // P자
        ]
      }
    }

    // 난이도별 스테이지 템플릿
    this.difficultyTemplates = {
      1: { // 초급 (1-100)
        obstacleRatio: 0.05, // 5% 장애물
        preplacedRatio: 0.1, // 10% 미리 배치된 블록
        availableBlocks: [1, 2, 3], // 작은 블록만
        timeMultiplier: 1.5, // 넉넉한 시간
        patterns: ['tutorial', 'corner_start']
      },

      2: { // 초중급 (101-200)
        obstacleRatio: 0.08,
        preplacedRatio: 0.15,
        availableBlocks: [1, 2, 3, 4],
        timeMultiplier: 1.3,
        patterns: ['guided', 'symmetric']
      },

      3: { // 중급 (201-400)
        obstacleRatio: 0.12,
        preplacedRatio: 0.2,
        availableBlocks: [1, 2, 3, 4, 5],
        timeMultiplier: 1.2,
        patterns: ['maze', 'puzzle']
      },

      4: { // 중상급 (401-600)
        obstacleRatio: 0.15,
        preplacedRatio: 0.25,
        availableBlocks: [2, 3, 4, 5],
        timeMultiplier: 1.1,
        patterns: ['complex', 'strategic']
      },

      5: { // 상급 (601-800)
        obstacleRatio: 0.18,
        preplacedRatio: 0.3,
        availableBlocks: [3, 4, 5],
        timeMultiplier: 1.0,
        patterns: ['expert', 'minimal']
      },

      6: { // 최상급 (801-1000)
        obstacleRatio: 0.22,
        preplacedRatio: 0.35,
        availableBlocks: [4, 5],
        timeMultiplier: 0.9,
        patterns: ['master', 'perfect']
      }
    }
  }

  /**
   * 특정 스테이지 번호에 대한 완전한 스테이지 데이터 생성
   * @param {number} stageNumber - 스테이지 번호 (1-1000+)
   * @returns {Object} 완전한 스테이지 데이터
   */
  generateStage (stageNumber) {
    // 시드 설정 (일관된 결과를 위해)
    const seed = this.createSeed(stageNumber)
    const random = this.createSeededRandom(seed)

    // 난이도 결정
    const difficulty = this.calculateDifficulty(stageNumber)
    const template = this.difficultyTemplates[difficulty]

    // 기본 정보 생성
    const metadata = this.generateMetadata(stageNumber, difficulty, template, random)

    // 보드 상태 생성
    const boardData = this.generateBoard(stageNumber, template, random)

    // 사용 가능한 블록 결정
    const availableBlocks = this.selectAvailableBlocks(stageNumber, template, random)

    // 힌트 생성
    const hints = this.generateHints(stageNumber, difficulty, boardData, random)

    return {
      // 메타데이터 (목록용)
      stage_number: stageNumber,
      title: metadata.title,
      difficulty,
      optimal_score: metadata.optimalScore,
      time_limit: metadata.timeLimit,
      thumbnail_url: `/thumbnails/stage_${String(stageNumber).padStart(4, '0')}.jpg`,
      preview_description: metadata.description,
      category: metadata.category,

      // 상세 게임 데이터
      initial_board_state: boardData.board,
      available_blocks: availableBlocks,
      hints,
      max_undo_count: Math.max(3, Math.floor(stageNumber / 50) + 2),

      // 특수 규칙
      special_rules: this.generateSpecialRules(stageNumber, difficulty, random),

      // 생성 정보 (디버깅용)
      generation_info: {
        seed,
        template_used: difficulty,
        obstacles_placed: boardData.obstacleCount,
        preplaced_blocks: boardData.preplacedCount,
        estimated_difficulty: boardData.estimatedDifficulty
      }
    }
  }

  /**
   * 스테이지 번호로부터 시드 생성
   */
  createSeed (stageNumber) {
    // 스테이지 번호와 고정 솔트를 조합하여 일관된 시드 생성
    const salt = 'BlokusStage2024'
    let hash = 0
    const str = `${salt}_${stageNumber}`

    for (let i = 0; i < str.length; i++) {
      const char = str.charCodeAt(i)
      hash = ((hash << 5) - hash + char) & 0xffffffff
    }

    return Math.abs(hash)
  }

  /**
   * 시드 기반 랜덤 생성기
   */
  createSeededRandom (seed) {
    let current = seed

    return {
      next: () => {
        current = (current * 1103515245 + 12345) & 0x7fffffff
        return current / 0x7fffffff
      },

      nextInt: (max) => {
        return Math.floor(this.createSeededRandom(current).next() * max)
      },

      choice: (array) => {
        const index = Math.floor(this.createSeededRandom(current).next() * array.length)
        return array[index]
      }
    }
  }

  /**
   * 스테이지 번호에 따른 난이도 계산
   */
  calculateDifficulty (stageNumber) {
    if (stageNumber <= 100) return 1 // 초급
    if (stageNumber <= 200) return 2 // 초중급
    if (stageNumber <= 400) return 3 // 중급
    if (stageNumber <= 600) return 4 // 중상급
    if (stageNumber <= 800) return 5 // 상급
    return 6 // 최상급
  }

  /**
   * 메타데이터 생성
   */
  generateMetadata (stageNumber, difficulty, template, random) {
    const baseScore = 50 + (stageNumber * 2)
    const difficultyBonus = difficulty * 10
    const optimalScore = baseScore + difficultyBonus + Math.floor(random.next() * 20)

    const baseTime = 120 // 2분 기본
    const stageTimeBonus = Math.floor(stageNumber / 20) * 15 // 20스테이지마다 15초 증가
    const timeLimit = Math.floor((baseTime + stageTimeBonus) * template.timeMultiplier)

    // 카테고리 결정
    let category
    if (stageNumber <= 50) category = 'tutorial'
    else if (stageNumber <= 200) category = 'basic'
    else if (stageNumber <= 600) category = 'intermediate'
    else category = 'advanced'

    // 특별한 스테이지들
    if (stageNumber % 100 === 0) category = 'milestone'
    if (stageNumber % 50 === 0 && stageNumber % 100 !== 0) category = 'challenge'

    // 제목과 설명 생성
    const titles = {
      1: ['첫 걸음', '기초 연습', '시작하기'],
      2: ['조금 더', '한 단계 위', '발전하기'],
      3: ['중간 지점', '실력 향상', '도전하기'],
      4: ['고급 퍼즐', '전략적 사고', '숙련하기'],
      5: ['전문가 도전', '완벽한 계획', '마스터하기'],
      6: ['궁극의 퍼즐', '완벽한 도전', '전설되기']
    }

    const titleOptions = titles[difficulty] || ['신비한 퍼즐']
    const title = `${titleOptions[Math.floor(random.next() * titleOptions.length)]} ${stageNumber}`

    const descriptions = [
      `${stageNumber}번째 블로쿠스 퍼즐에 도전하세요!`,
      `난이도 ${difficulty}의 전략적 퍼즐입니다.`,
      '창의적 사고가 필요한 도전입니다.',
      '완벽한 배치를 찾아보세요.'
    ]

    return {
      title,
      optimalScore,
      timeLimit,
      description: descriptions[Math.floor(random.next() * descriptions.length)],
      category
    }
  }

  /**
   * 보드 상태 생성 (장애물 + 미리 배치된 블록)
   */
  generateBoard (stageNumber, template, random) {
    const board = Array(20).fill(null).map(() => Array(20).fill(0))
    let obstacleCount = 0
    let preplacedCount = 0

    // 1단계: 장애물 배치
    const totalCells = 20 * 20
    const targetObstacles = Math.floor(totalCells * template.obstacleRatio)

    // 패턴별 장애물 배치
    const patterns = template.patterns
    const selectedPattern = patterns[Math.floor(random.next() * patterns.length)]

    switch (selectedPattern) {
      case 'tutorial':
        obstacleCount = this.placeTutorialObstacles(board, targetObstacles, random)
        break

      case 'corner_start':
        obstacleCount = this.placeCornerObstacles(board, targetObstacles, random)
        break

      case 'symmetric':
        obstacleCount = this.placeSymmetricObstacles(board, targetObstacles, random)
        break

      case 'maze':
        obstacleCount = this.placeMazeObstacles(board, targetObstacles, random)
        break

      default:
        obstacleCount = this.placeRandomObstacles(board, targetObstacles, random)
    }

    // 2단계: 미리 배치된 블록 (플레이어 색상)
    const targetPreplaced = Math.floor(totalCells * template.preplacedRatio)
    preplacedCount = this.placePreplacedBlocks(board, targetPreplaced, template.availableBlocks, random)

    // 3단계: 난이도 추정
    const estimatedDifficulty = this.estimateBoardDifficulty(board, obstacleCount, preplacedCount)

    return {
      board,
      obstacleCount,
      preplacedCount,
      estimatedDifficulty,
      patternUsed: selectedPattern
    }
  }

  /**
   * 튜토리얼용 장애물 배치 (간단하고 명확한 패턴)
   */
  placeTutorialObstacles (board, targetCount, random) {
    let placed = 0

    // 보드 가장자리에 몇 개 배치
    const edges = [
      { x: 0, y: 10 }, { x: 19, y: 10 },
      { x: 10, y: 0 }, { x: 10, y: 19 }
    ]

    for (const pos of edges) {
      if (placed >= targetCount) break
      board[pos.y][pos.x] = -1 // 장애물
      placed++
    }

    // 나머지는 랜덤하게
    while (placed < targetCount) {
      const x = Math.floor(random.next() * 20)
      const y = Math.floor(random.next() * 20)

      if (board[y][x] === 0) {
        board[y][x] = -1
        placed++
      }
    }

    return placed
  }

  /**
   * 코너 시작 패턴 (각 코너에서 시작하도록 유도)
   */
  placeCornerObstacles (board, targetCount, random) {
    let placed = 0

    // 코너 근처에 장애물 배치하여 시작점을 명확하게
    const cornerAreas = [
      { x: 1, y: 1, w: 3, h: 3 }, // 왼쪽 위
      { x: 16, y: 1, w: 3, h: 3 }, // 오른쪽 위
      { x: 1, y: 16, w: 3, h: 3 }, // 왼쪽 아래
      { x: 16, y: 16, w: 3, h: 3 } // 오른쪽 아래
    ]

    for (const area of cornerAreas) {
      if (placed >= targetCount) break

      // 각 영역에 1-2개 장애물 배치
      const countInArea = 1 + Math.floor(random.next() * 2)
      for (let i = 0; i < countInArea && placed < targetCount; i++) {
        const x = area.x + Math.floor(random.next() * area.w)
        const y = area.y + Math.floor(random.next() * area.h)

        if (board[y][x] === 0) {
          board[y][x] = -1
          placed++
        }
      }
    }

    return placed
  }

  /**
   * 대칭 패턴 장애물
   */
  placeSymmetricObstacles (board, targetCount, random) {
    let placed = 0
    const centerX = 10
    const centerY = 10

    while (placed < targetCount - 1) { // 짝수 개씩 배치하기 위해
      const x = Math.floor(random.next() * 10) // 왼쪽 반만
      const y = Math.floor(random.next() * 20)

      if (board[y][x] === 0) {
        board[y][x] = -1
        placed++

        // 대칭 위치에도 배치
        const symX = 19 - x
        if (board[y][symX] === 0) {
          board[y][symX] = -1
          placed++
        }
      }
    }

    return placed
  }

  /**
   * 미로 패턴 장애물
   */
  placeMazeObstacles (board, targetCount, random) {
    let placed = 0

    // 격자 패턴으로 장애물 배치
    for (let y = 2; y < 18; y += 4) {
      for (let x = 2; x < 18; x += 4) {
        if (placed >= targetCount) break

        // 십자 모양으로 장애물 배치
        const positions = [
          { x, y }, { x: x + 1, y }, { x: x - 1, y }, { x, y: y + 1 }, { x, y: y - 1 }
        ]

        for (const pos of positions) {
          if (placed >= targetCount) break
          if (pos.x >= 0 && pos.x < 20 && pos.y >= 0 && pos.y < 20) {
            if (board[pos.y][pos.x] === 0) {
              board[pos.y][pos.x] = -1
              placed++
            }
          }
        }
      }
    }

    return placed
  }

  /**
   * 랜덤 장애물 배치
   */
  placeRandomObstacles (board, targetCount, random) {
    let placed = 0

    while (placed < targetCount) {
      const x = Math.floor(random.next() * 20)
      const y = Math.floor(random.next() * 20)

      if (board[y][x] === 0) {
        board[y][x] = -1
        placed++
      }
    }

    return placed
  }

  /**
   * 미리 배치된 플레이어 블록 배치
   */
  placePreplacedBlocks (board, targetCount, availableBlocks, random) {
    let placed = 0

    // 플레이어 색상 (1-4, 각 플레이어별)
    const playerColors = [1, 2, 3, 4]

    while (placed < targetCount) {
      const x = Math.floor(random.next() * 20)
      const y = Math.floor(random.next() * 20)
      const color = playerColors[Math.floor(random.next() * playerColors.length)]

      if (board[y][x] === 0) {
        board[y][x] = color
        placed++
      }
    }

    return placed
  }

  /**
   * 보드 난이도 추정
   */
  estimateBoardDifficulty (board, obstacleCount, preplacedCount) {
    let difficulty = 0

    // 장애물 밀도에 따른 난이도
    difficulty += obstacleCount * 0.1

    // 미리 배치된 블록에 따른 난이도 (많을수록 제약이 많음)
    difficulty += preplacedCount * 0.05

    // 연결성 분석 (빈 공간이 얼마나 연결되어 있는가)
    const connectivity = this.analyzeConnectivity(board)
    difficulty += (1 - connectivity) * 20 // 연결성이 낮을수록 어려움

    return Math.min(100, Math.max(0, difficulty))
  }

  /**
   * 보드 연결성 분석
   */
  analyzeConnectivity (board) {
    const visited = Array(20).fill(null).map(() => Array(20).fill(false))
    let totalEmpty = 0
    let largestComponent = 0

    // 모든 빈 칸 수 계산
    for (let y = 0; y < 20; y++) {
      for (let x = 0; x < 20; x++) {
        if (board[y][x] === 0) totalEmpty++
      }
    }

    // 가장 큰 연결 컴포넌트 찾기
    for (let y = 0; y < 20; y++) {
      for (let x = 0; x < 20; x++) {
        if (board[y][x] === 0 && !visited[y][x]) {
          const componentSize = this.dfs(board, visited, x, y)
          largestComponent = Math.max(largestComponent, componentSize)
        }
      }
    }

    return totalEmpty > 0 ? largestComponent / totalEmpty : 0
  }

  /**
   * DFS를 사용한 연결 컴포넌트 크기 계산
   */
  dfs (board, visited, x, y) {
    if (x < 0 || x >= 20 || y < 0 || y >= 20) return 0
    if (visited[y][x] || board[y][x] !== 0) return 0

    visited[y][x] = true
    let size = 1

    // 4방향으로 확산
    const directions = [{ x: 0, y: 1 }, { x: 1, y: 0 }, { x: 0, y: -1 }, { x: -1, y: 0 }]
    for (const dir of directions) {
      size += this.dfs(board, visited, x + dir.x, y + dir.y)
    }

    return size
  }

  /**
   * 사용 가능한 블록 선택
   */
  selectAvailableBlocks (stageNumber, template, random) {
    const baseBlocks = template.availableBlocks

    // 스테이지가 진행될수록 더 많은 블록 추가
    let blockCount = baseBlocks.length
    if (stageNumber > 500) blockCount = Math.min(blockCount + 1, 5)
    if (stageNumber > 800) blockCount = Math.min(blockCount + 1, 5)

    // 랜덤하게 일부 블록 제외 (난이도 조절)
    const availableBlocks = [...baseBlocks]
    while (availableBlocks.length > blockCount && availableBlocks.length > 2) {
      const removeIndex = Math.floor(random.next() * availableBlocks.length)
      availableBlocks.splice(removeIndex, 1)
    }

    return availableBlocks.sort((a, b) => a - b)
  }

  /**
   * 힌트 생성
   */
  generateHints (stageNumber, difficulty, boardData, random) {
    const hints = []

    // 기본 힌트들
    const basicHints = [
      '코너에서 시작하는 것이 유리합니다',
      '큰 블록부터 배치해보세요',
      '다른 플레이어의 블록과 대각선으로만 연결하세요',
      '빈 공간을 효율적으로 활용하세요'
    ]

    // 난이도별 특별 힌트
    const difficultyHints = {
      1: ['천천히 생각해보세요', '실수해도 괜찮습니다'],
      2: ['여러 가지 방법을 시도해보세요', '패턴을 찾아보세요'],
      3: ['전략적으로 접근하세요', '상대방의 움직임을 예상하세요'],
      4: ['완벽한 계획이 필요합니다', '모든 가능성을 고려하세요'],
      5: ['최적화가 핵심입니다', '창의적 사고가 필요합니다'],
      6: ['마스터 레벨의 도전입니다', '완벽함을 추구하세요']
    }

    // 기본 힌트 추가
    hints.push(basicHints[Math.floor(random.next() * basicHints.length)])

    // 난이도별 힌트 추가
    const diffHints = difficultyHints[difficulty] || []
    if (diffHints.length > 0) {
      hints.push(diffHints[Math.floor(random.next() * diffHints.length)])
    }

    // 패턴별 힌트
    if (boardData.patternUsed) {
      const patternHints = {
        tutorial: '기본기를 다져보세요',
        corner_start: '코너 활용이 중요합니다',
        symmetric: '대칭성을 활용해보세요',
        maze: '경로를 신중하게 계획하세요'
      }

      if (patternHints[boardData.patternUsed]) {
        hints.push(patternHints[boardData.patternUsed])
      }
    }

    return hints
  }

  /**
   * 특수 규칙 생성
   */
  generateSpecialRules (stageNumber, difficulty, random) {
    const rules = {
      time_pressure: false,
      limited_undos: false,
      bonus_multiplier: 1.0,
      special_scoring: null
    }

    // 특별한 스테이지들에 특수 규칙 적용
    if (stageNumber % 100 === 0) {
      // 100의 배수: 보너스 점수
      rules.bonus_multiplier = 1.5
      rules.special_scoring = 'milestone_bonus'
    }

    if (stageNumber % 50 === 0 && stageNumber % 100 !== 0) {
      // 50의 배수 (100 제외): 시간 압박
      rules.time_pressure = true
    }

    if (difficulty >= 5 && stageNumber % 25 === 0) {
      // 고난이도 + 25의 배수: 제한된 되돌리기
      rules.limited_undos = true
    }

    // 랜덤 특수 효과 (낮은 확률)
    if (random.next() < 0.05) { // 5% 확률
      const specialEffects = [
        { bonus_multiplier: 1.2, special_scoring: 'perfect_bonus' },
        { time_pressure: true },
        { limited_undos: true }
      ]

      const effect = specialEffects[Math.floor(random.next() * specialEffects.length)]
      Object.assign(rules, effect)
    }

    return rules
  }

  /**
   * 스테이지 유효성 검증
   */
  validateStage (stageData) {
    const issues = []

    // 기본 검증
    if (!stageData.initial_board_state) {
      issues.push('초기 보드 상태가 없습니다')
    }

    if (!stageData.available_blocks || stageData.available_blocks.length === 0) {
      issues.push('사용 가능한 블록이 없습니다')
    }

    if (stageData.time_limit < 60) {
      issues.push('시간 제한이 너무 짧습니다')
    }

    // 보드 검증
    if (stageData.initial_board_state) {
      const board = stageData.initial_board_state
      let emptyCount = 0

      for (let y = 0; y < 20; y++) {
        for (let x = 0; x < 20; x++) {
          if (board[y][x] === 0) emptyCount++
        }
      }

      if (emptyCount < 50) {
        issues.push('빈 공간이 너무 적습니다')
      }

      if (emptyCount > 350) {
        issues.push('빈 공간이 너무 많습니다')
      }
    }

    return {
      isValid: issues.length === 0,
      issues
    }
  }

  /**
   * 여러 스테이지 일괄 생성 및 검증
   */
  generateMultipleStages (startNumber, count) {
    const stages = []
    const validationResults = []

    logger.info(`Generating ${count} stages starting from ${startNumber}`)

    for (let i = 0; i < count; i++) {
      const stageNumber = startNumber + i

      try {
        const stage = this.generateStage(stageNumber)
        const validation = this.validateStage(stage)

        stages.push(stage)
        validationResults.push({
          stageNumber,
          isValid: validation.isValid,
          issues: validation.issues
        })

        if (!validation.isValid) {
          logger.warn(`Stage ${stageNumber} has validation issues:`, validation.issues)
        }
      } catch (error) {
        logger.error(`Failed to generate stage ${stageNumber}:`, error)
        validationResults.push({
          stageNumber,
          isValid: false,
          issues: [`Generation failed: ${error.message}`]
        })
      }
    }

    const validStages = validationResults.filter(r => r.isValid).length
    logger.info(`Generated ${stages.length} stages, ${validStages} valid`)

    return {
      stages,
      validationResults,
      summary: {
        total: stages.length,
        valid: validStages,
        invalid: stages.length - validStages
      }
    }
  }
}

module.exports = StageGenerator
