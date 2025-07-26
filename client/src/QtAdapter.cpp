#include "ClientTypes.h"
#include "QtAdapter.h"
#include "Utils.h"
#include <QDebug>

namespace Blokus {

    // ========================================
    // QtAdapter 네임스페이스 구현
    // ========================================
    namespace QtAdapter {

        QRect boundingRectToQRect(const Common::Block::BoundingRect& rect) {
            return QRect(rect.left, rect.top, rect.width, rect.height);
        }

    } // namespace QtAdapter


} // namespace Blokus