-- PostCategory enum 생성
CREATE TYPE "PostCategory" AS ENUM ('QUESTION', 'GUIDE', 'GENERAL');

-- posts 테이블 생성
CREATE TABLE "posts" (
    "id" SERIAL PRIMARY KEY,
    "title" VARCHAR(200) NOT NULL,
    "content" TEXT NOT NULL,
    "category" "PostCategory" NOT NULL,
    "author_id" INTEGER NOT NULL,
    "is_hidden" BOOLEAN NOT NULL DEFAULT false,
    "is_deleted" BOOLEAN NOT NULL DEFAULT false,
    "view_count" INTEGER NOT NULL DEFAULT 0,
    "created_at" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updated_at" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    
    CONSTRAINT "posts_author_id_fkey" FOREIGN KEY ("author_id") REFERENCES "users"("user_id") ON DELETE CASCADE
);

-- 인덱스 생성
CREATE INDEX "posts_category_idx" ON "posts"("category");
CREATE INDEX "posts_author_id_idx" ON "posts"("author_id");
CREATE INDEX "posts_created_at_idx" ON "posts"("created_at");
CREATE INDEX "posts_is_deleted_is_hidden_idx" ON "posts"("is_deleted", "is_hidden");