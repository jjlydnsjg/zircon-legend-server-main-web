#!/bin/bash
# 多架构 Docker 镜像构建脚本
#
# 前置条件:
#   1. 安装 Docker Buildx: docker buildx create --use
#   2. 启用 QEMU 模拟器（用于跨架构构建）:
#      docker run --privileged --rm tonistiigi/binfmt --install all
#
# 用法:
#   ./scripts/build-multiarch.sh [选项]
#
# 选项:
#   --arm64     仅构建 ARM64 镜像
#   --amd64     仅构建 x64 镜像
#   --all       构建多架构镜像 (默认)
#   --push      构建并推送到 Docker Hub
#   --tag TAG   指定镜像标签 (默认: zircon:latest)

set -e

# 默认值
PLATFORM="linux/amd64,linux/arm64"
PUSH=""
TAG="zircon:latest"

# 解析参数
while [[ $# -gt 0 ]]; do
    case $1 in
        --arm64)
            PLATFORM="linux/arm64"
            TAG="zircon:arm64"
            shift
            ;;
        --amd64)
            PLATFORM="linux/amd64"
            TAG="zircon:amd64"
            shift
            ;;
        --all)
            PLATFORM="linux/amd64,linux/arm64"
            shift
            ;;
        --push)
            PUSH="--push"
            shift
            ;;
        --tag)
            TAG="$2"
            shift 2
            ;;
        *)
            echo "未知选项: $1"
            exit 1
            ;;
    esac
done

# 切换到项目根目录
cd "$(dirname "$0")/.."

echo "=========================================="
echo "构建多架构 Docker 镜像"
echo "=========================================="
echo "平台: $PLATFORM"
echo "标签: $TAG"
echo "推送: ${PUSH:-否}"
echo "=========================================="

# 检查 buildx 是否可用
if ! docker buildx version > /dev/null 2>&1; then
    echo "错误: Docker Buildx 未安装"
    echo "请运行: docker buildx create --use"
    exit 1
fi

# 创建/使用 builder 实例
BUILDER_NAME="multiarch-builder"
if ! docker buildx inspect "$BUILDER_NAME" > /dev/null 2>&1; then
    echo "创建 buildx builder: $BUILDER_NAME"
    docker buildx create --name "$BUILDER_NAME" --use
fi
docker buildx use "$BUILDER_NAME"

# 构建镜像
echo ""
echo "开始构建..."
if [ -n "$PUSH" ]; then
    docker buildx build \
        --platform "$PLATFORM" \
        -t "$TAG" \
        -f Dockerfile.arm64 \
        $PUSH \
        .
else
    # 本地构建需要 --load（仅支持单一架构）
    if [[ "$PLATFORM" == *","* ]]; then
        echo "提示: 多架构本地构建不支持 --load，将使用 --output type=image"
        docker buildx build \
            --platform "$PLATFORM" \
            -t "$TAG" \
            -f Dockerfile.arm64 \
            --output type=image \
            .
    else
        docker buildx build \
            --platform "$PLATFORM" \
            -t "$TAG" \
            -f Dockerfile.arm64 \
            --load \
            .
    fi
fi

echo ""
echo "=========================================="
echo "构建完成!"
echo "=========================================="
