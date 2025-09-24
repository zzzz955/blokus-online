#pragma once

#include "ClientBlock.h"
#include "ClientTypes.h"
#include <QRect>

namespace Blokus {
    namespace QtAdapter {

        //  BoundingRect → QRect 변환 함수
        QRect boundingRectToQRect(const Common::Block::BoundingRect& rect);

    } // namespace QtAdapter
} // namespace Blokus