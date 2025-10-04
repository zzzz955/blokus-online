import re
import os
from pathlib import Path

def parse_unity_file(file_path):
    """Parse Unity scene/prefab file to extract GameObjects with Image components."""
    results = []

    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()

    # Step 1: Find all GameObject sections (more precise matching)
    # Match only up to the next separator line to avoid cross-section matching
    gameobject_pattern = r'--- !u!1 &(\d+)\nGameObject:\n(?:(?!---).)*?m_Name: ([^\n]+)'
    gameobjects = {}
    for match in re.finditer(gameobject_pattern, content, re.DOTALL):
        obj_id = match.group(1)
        obj_name = match.group(2).strip()
        gameobjects[obj_id] = {
            'name': obj_name,
            'size': None,
            'parent_id': None,
            'has_image': False
        }

    # Step 2: Find all RectTransform sections with size and parent data (more precise)
    recttransform_pattern = r'--- !u!224 &\d+\nRectTransform:\n(?:(?!---).)*?m_GameObject: \{fileID: (\d+)\}(?:(?!---).)*?m_Father: \{fileID: (\d+)\}(?:(?!---).)*?m_SizeDelta: \{x: ([^,]+), y: ([^}]+)\}'
    for match in re.finditer(recttransform_pattern, content, re.DOTALL):
        obj_id = match.group(1)
        parent_id = match.group(2)
        width = float(match.group(3))
        height = float(match.group(4))

        if obj_id in gameobjects:
            gameobjects[obj_id]['size'] = (width, height)
            gameobjects[obj_id]['parent_id'] = parent_id if parent_id != '0' else None

    # Step 3: Find all Image components and mark GameObjects (more precise)
    # Only match within the same MonoBehaviour section, not across sections
    image_pattern = r'--- !u!114 &\d+\nMonoBehaviour:\n(?:(?!---).)*?m_GameObject: \{fileID: (\d+)\}(?:(?!---).)*?m_Script: \{fileID: 11500000, guid: fe87c0e1cc204ed48ad3b37840f39efc'
    for match in re.finditer(image_pattern, content, re.DOTALL):
        obj_id = match.group(1)
        if obj_id in gameobjects:
            gameobjects[obj_id]['has_image'] = True

    # Step 4: Calculate actual sizes (resolve 0.0 sizes from parents)
    def get_actual_size(obj_id, visited=None):
        """Recursively get actual size, inheriting from parent if size is 0."""
        if visited is None:
            visited = set()

        if obj_id not in gameobjects or obj_id in visited:
            return None

        visited.add(obj_id)
        obj = gameobjects[obj_id]

        if obj['size'] is None:
            return None

        width, height = obj['size']

        # If both dimensions are non-zero, return as-is
        if width != 0.0 and height != 0.0:
            return (width, height)

        # If one or both dimensions are 0, try to get from parent
        parent_size = None
        if obj['parent_id']:
            parent_size = get_actual_size(obj['parent_id'], visited)

        if parent_size:
            # Use parent size for 0 dimensions
            actual_width = width if width != 0.0 else parent_size[0]
            actual_height = height if height != 0.0 else parent_size[1]
            return (actual_width, actual_height)

        # If no parent or parent has no size, return original
        return (width, height)

    # Step 5: Filter only GameObjects with Image components directly attached
    for obj_id, data in gameobjects.items():
        if data['has_image']:
            actual_size = get_actual_size(obj_id)
            results.append({
                'name': data['name'],
                'size': actual_size,
                'file': os.path.basename(file_path)
            })

    return results

def analyze_role(name, file_name):
    """Infer the role/purpose based on GameObject name and context."""
    name_lower = name.lower()

    # Common UI patterns
    if 'background' in name_lower or 'bg' in name_lower:
        return "배경 이미지"
    elif 'panel' in name_lower:
        return "패널 컨테이너"
    elif 'button' in name_lower or 'btn' in name_lower:
        return "버튼 이미지/배경"
    elif 'icon' in name_lower:
        return "아이콘 이미지"
    elif 'avatar' in name_lower or 'profile' in name_lower:
        return "프로필/아바타 이미지"
    elif 'slot' in name_lower:
        return "슬롯 이미지"
    elif 'header' in name_lower:
        return "헤더 이미지"
    elif 'footer' in name_lower:
        return "푸터 이미지"
    elif 'border' in name_lower or 'frame' in name_lower:
        return "테두리/프레임"
    elif 'shadow' in name_lower:
        return "그림자 효과"
    elif 'overlay' in name_lower:
        return "오버레이 효과"
    elif 'divider' in name_lower or 'separator' in name_lower:
        return "구분선"
    elif 'indicator' in name_lower:
        return "인디케이터"
    elif 'badge' in name_lower:
        return "뱃지"
    elif 'toast' in name_lower:
        return "토스트 메시지 배경"
    elif 'modal' in name_lower or 'popup' in name_lower:
        return "모달/팝업 배경"
    elif 'chat' in name_lower:
        return "채팅 UI 요소"
    elif 'room' in name_lower:
        return "방 UI 요소"
    elif 'player' in name_lower:
        return "플레이어 UI 요소"
    elif 'block' in name_lower:
        return "블록 게임 요소"
    elif 'rank' in name_lower or 'ranking' in name_lower:
        return "랭킹 UI 요소"
    elif 'image' in name_lower:
        return "일반 이미지"
    else:
        return "UI 이미지 요소"

def main():
    # Find all scene and prefab files
    scene_files = list(Path('Assets/_Project/Scenes').glob('*.unity'))
    prefab_files = list(Path('Assets/_Project/Prefabs').glob('*.prefab'))

    all_files = scene_files + prefab_files
    all_results = []

    print(f"Analyzing {len(all_files)} files...")

    for file_path in all_files:
        try:
            results = parse_unity_file(file_path)
            for result in results:
                result['role'] = analyze_role(result['name'], result['file'])
                all_results.append(result)
            print(f"[OK] {file_path.name}: {len(results)} Images found")
        except Exception as e:
            print(f"[ERROR] {file_path.name}: {e}")

    # Sort by file and name
    all_results.sort(key=lambda x: (x['file'], x['name']))

    # Write to UI.txt
    with open('UI.txt', 'w', encoding='utf-8') as f:
        f.write("=" * 80 + "\n")
        f.write("Unity 프로젝트 Image 컴포넌트 분석 결과\n")
        f.write("=" * 80 + "\n\n")
        f.write(f"총 {len(all_results)}개의 Image 컴포넌트가 직접 붙은 GameObject 발견\n\n")
        f.write("* 크기가 0.0인 경우 부모 오브젝트의 크기를 상속받아 표시됨\n")
        f.write("* Image 컴포넌트가 직접 붙은 오브젝트만 표시\n\n")

        # Group by file
        current_file = None
        for item in all_results:
            if item['file'] != current_file:
                current_file = item['file']
                f.write("\n" + "-" * 80 + "\n")
                f.write(f"파일: {current_file}\n")
                f.write("-" * 80 + "\n\n")

            f.write(f"GameObject 이름: {item['name']}\n")
            if item['size']:
                width, height = item['size']
                if width == 0.0 and height == 0.0:
                    f.write(f"  크기: (전체 화면 크기에 맞춤)\n")
                elif width == 0.0:
                    f.write(f"  크기: (부모 너비) x {height:.1f}\n")
                elif height == 0.0:
                    f.write(f"  크기: {width:.1f} x (부모 높이)\n")
                else:
                    f.write(f"  크기: {width:.1f} x {height:.1f}\n")
            else:
                f.write(f"  크기: (크기 정보 없음)\n")
            f.write(f"  역할: {item['role']}\n")
            f.write("\n")

        f.write("\n" + "=" * 80 + "\n")
        f.write("분석 완료\n")
        f.write("=" * 80 + "\n")

    print(f"\n[DONE] Saved {len(all_results)} items to UI.txt")

if __name__ == '__main__':
    main()
