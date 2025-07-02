#pragma once

// Common 라이브러리의 GameLogic을 import
#include "../../../common/include/common/GameLogic.h"
#include "common/Types.h"

namespace Blokus {

    // ========================================
    // 기존 코드 호환성을 위한 alias
    // ========================================

    // Common 라이브러리의 GameLogic과 GameStateManager를 기본 네임스페이스로 가져오기
    using GameLogic = Common::GameLogic;
    using GameStateManager = Common::GameStateManager;

    // ========================================
    // Qt 클라이언트 전용 확장 클래스들 (필요한 경우)
    // ========================================

    // 향후 클라이언트 전용 기능이 필요한 경우를 위한 래퍼 클래스
    class QtGameLogic : public Common::GameLogic
    {
    public:
        QtGameLogic() : Common::GameLogic() {}

        // Qt 전용 편의 함수들 (필요시 추가)
        QString getPlayerColorString(PlayerColor player) const {
            return Utils::playerColorToString(player);
        }

        QColor getPlayerQColor(PlayerColor player) const {
            return Utils::getPlayerColor(player);
        }

        // 기존 인터페이스와의 호환성을 위한 오버로드
        bool canPlaceBlock(const Block& block, const Position& position, PlayerColor player) const {
            BlockPlacement placement;
            placement.type = block.getType();
            placement.position = position;
            placement.rotation = block.getRotation();
            placement.flip = block.getFlipState();
            placement.player = player;

            return Common::GameLogic::canPlaceBlock(placement);
        }

        bool placeBlock(const Block& block, const Position& position, PlayerColor player) {
            BlockPlacement placement;
            placement.type = block.getType();
            placement.position = position;
            placement.rotation = block.getRotation();
            placement.flip = block.getFlipState();
            placement.player = player;

            return Common::GameLogic::placeBlock(placement);
        }
    };

} // namespace Blokus