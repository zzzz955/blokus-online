# 다크 테마 UX 가이드라인

## 개요
이 문서는 Blokus Online 웹 애플리케이션에서 다크 테마를 적용할 때 일관성 있고 사용자 친화적인 UX를 제공하기 위한 가이드라인입니다.

## 🎨 색상 시스템

### 주요 배경색
```css
/* 기본 다크 배경 */
bg-dark-bg      /* #111827 - 주요 배경 */
bg-dark-card    /* #1f2937 - 카드/컨테이너 배경 */
bg-dark-border  /* #374151 - 경계선 */

/* 상호작용 요소 */
bg-gray-700     /* 비선택 상태 버튼 배경 */
bg-gray-600     /* 호버 상태 */
bg-blue-600     /* 선택/활성 상태 */
```

### 텍스트 색상
```css
/* 다크 배경용 텍스트 */
text-white      /* #ffffff - 주요 텍스트 */
text-gray-300   /* #d1d5db - 보조 텍스트 */
text-gray-400   /* #9ca3af - 레이블, 메타 정보 */

/* 밝은 배경용 텍스트 (사용 주의) */
text-gray-900   /* #111827 - 밝은 배경의 주요 텍스트 */
text-gray-700   /* #374151 - 밝은 배경의 보조 텍스트 */
```

## ⚠️ 주의사항 및 문제 해결

### 1. 색상 대비 문제
❌ **잘못된 예시:**
```tsx
// 밝은 배경 + 밝은 텍스트 = 가독성 문제
<button className="bg-gray-100 text-white">버튼</button>

// 어두운 배경 + 어두운 텍스트 = 가독성 문제  
<div className="bg-dark-bg text-gray-800">텍스트</div>
```

✅ **올바른 예시:**
```tsx
// 밝은 배경 + 어두운 텍스트
<button className="bg-gray-100 text-gray-900">버튼</button>

// 어두운 배경 + 밝은 텍스트
<div className="bg-dark-bg text-white">텍스트</div>
```

### 2. 상호작용 요소 색상
✅ **버튼 상태별 색상:**
```tsx
// 선택되지 않은 상태
className="bg-gray-700 text-gray-300 hover:bg-gray-600"

// 선택된 상태  
className="bg-blue-600 text-white"

// 호버 효과
className="hover:bg-gray-700/30"  // 투명도 활용
```

### 3. 테이블 및 리스트 요소
```tsx
// 테이블 헤더
<thead className="bg-dark-bg">

// 테이블 행 호버
<tr className="hover:bg-gray-700/30">

// 카드 배경
<div className="bg-dark-card border border-dark-border">
```

## 📋 개발 시 체크리스트

### 기본 체크리스트
- [ ] 모든 텍스트가 배경색과 **충분한 대비**를 가지는가?
- [ ] **호버 상태**에서도 텍스트가 명확히 보이는가?
- [ ] **선택된 상태**와 **선택되지 않은 상태**가 구분되는가?
- [ ] **포커스 상태**가 명확히 표시되는가?

### 상세 체크리스트
- [ ] 버튼: `bg-gray-700 text-gray-300` (비선택) → `bg-blue-600 text-white` (선택)
- [ ] 호버: 배경색에 맞는 적절한 명도 변경 (`hover:bg-gray-600`, `hover:bg-gray-700/30`)
- [ ] 테이블: 헤더 `bg-dark-bg`, 행 호버 `hover:bg-gray-700/30`
- [ ] 모달: `bg-dark-card border border-dark-border`
- [ ] 입력 필드: 다크 테마에 맞는 스타일 적용

## 🔧 일반적인 수정 패턴

### 1. 버튼 색상 수정
```tsx
// Before
className={`${selected ? 'bg-blue-600 text-white' : 'bg-gray-100 text-white'}`}

// After  
className={`${selected ? 'bg-blue-600 text-white' : 'bg-gray-700 text-gray-300 hover:bg-gray-600'}`}
```

### 2. 테이블 호버 효과 수정
```tsx
// Before
<tr className="hover:bg-gray-50">

// After
<tr className="hover:bg-gray-700/30">
```

### 3. 카드/모달 배경 수정
```tsx
// Before  
<div className="bg-white border border-gray-200">

// After
<div className="bg-dark-card border border-dark-border">
```

## 🎯 핵심 원칙

1. **대비 원칙**: 밝은 배경 → 어두운 텍스트, 어두운 배경 → 밝은 텍스트
2. **일관성 원칙**: 동일한 기능의 UI 요소는 동일한 색상 조합 사용
3. **접근성 원칙**: WCAG 2.1 AA 기준 (4.5:1) 이상의 색상 대비 유지
4. **사용성 원칙**: 호버, 포커스, 선택 상태가 명확히 구분되어야 함

## 🚀 권장 도구

### 색상 대비 검사
- [WebAIM Contrast Checker](https://webaim.org/resources/contrastchecker/)
- Chrome DevTools의 Accessibility 패널
- [Colour Contrast Analyser (CCA)](https://www.tpgi.com/color-contrast-checker/)

### Tailwind CSS 클래스 참조
```css
/* 다크 테마 색상 팔레트 */
gray-50:   #f9fafb
gray-100:  #f3f4f6  
gray-300:  #d1d5db
gray-400:  #9ca3af
gray-600:  #4b5563
gray-700:  #374151
gray-800:  #1f2937
gray-900:  #111827
```

## 📝 예제 코드

### 완전한 다크 테마 버튼 컴포넌트
```tsx
interface ButtonProps {
  variant?: 'primary' | 'secondary' | 'filter';
  selected?: boolean;
  children: React.ReactNode;
  onClick?: () => void;
}

const Button: React.FC<ButtonProps> = ({ variant = 'primary', selected = false, children, onClick }) => {
  const baseClasses = "px-3 py-1 rounded-lg text-sm font-medium transition-colors";
  
  const variantClasses = {
    primary: selected 
      ? "bg-blue-600 text-white hover:bg-blue-700" 
      : "bg-gray-700 text-gray-300 hover:bg-gray-600",
    secondary: "bg-gray-600 text-white hover:bg-gray-500",
    filter: selected
      ? "bg-blue-600 text-white"
      : "bg-gray-700 text-gray-300 hover:bg-gray-600"
  };

  return (
    <button 
      className={`${baseClasses} ${variantClasses[variant]}`}
      onClick={onClick}
    >
      {children}
    </button>
  );
};
```

---

**마지막 업데이트**: 2024년 12월
**버전**: 1.0
**담당자**: Blokus Online 개발팀