#!/bin/bash
set -e

# 出错时也暂停，避免窗口一闪而过看不到错误
trap 'echo ""; echo "⚠ 脚本出错退出"; read -p "按回车键退出..."' ERR

# 检测到冲突时提示并退出（不执行 pull）
die() {
  echo ""
  echo "$1"
  read -p "按回车键退出..."
  exit 1
}

echo "==> 预检查冲突"
git fetch

if [ -n "$(git rev-list HEAD..origin/main)" ]; then
  echo "    远端有新提交，检查是否冲突..."

  # 1. 模拟合并本地提交与远端提交，检测 rebase 冲突（只读，不改动工作区）
  if git merge-tree "$(git merge-base HEAD origin/main)" HEAD origin/main 2>/dev/null | grep -q '<<<<<<<'; then
    die "⚠ 检测到 rebase 冲突：本地提交与远端提交改动冲突
   已取消同步，请先手动解决（如 git rebase origin/main）后重试"
  fi

  # 2. 工作区改动与远端改动重叠则 stash pop 会冲突
  OVERLAP=$(grep -Fx -f <(git diff --name-only HEAD) <(git diff --name-only HEAD origin/main) || true)
  if [ -n "$OVERLAP" ]; then
    die "⚠ 检测到 stash pop 冲突：工作区改动与远端改动重叠
   重叠文件：
$OVERLAP
   已取消同步，请先提交或暂存这些改动后重试"
  fi

  echo "    无冲突"
else
  echo "    无新提交需要拉取"
fi

echo "==> git stash"
if git diff --quiet && git diff --cached --quiet; then
  echo "    工作区干净，跳过 stash"
  STASHED=false
else
  git stash push -m "auto-stash before sync $(date +%Y%m%d-%H%M%S)"
  STASHED=true
fi

echo "==> git pull --rebase"
git pull --rebase

if $STASHED; then
  echo "==> git stash pop"
  if ! git stash pop; then
    echo "⚠  stash pop 冲突！请手动解决后 git stash drop"
    read -p "按回车键退出..."
    exit 1
  fi
fi

echo "==> git push"
git push

echo "✔ 同步完成"
