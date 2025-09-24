#pragma once

#include "GameLogic.h"
#include "ClientTypes.h"

namespace Blokus {

    // Common 클래스들을 그대로 사용
    using GameLogic = Common::GameLogic;
    using GameStateManager = Common::GameStateManager;

    //  기존 클라이언트 코드 호환성을 위한 래퍼 클래스
    class QtGameLogic : public Common::GameLogic
    {
    public:
        QtGameLogic() : Common::GameLogic() {}

        //  기존 인터페이스 호환성을 위한 오버로드
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