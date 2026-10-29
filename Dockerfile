# The docs site of a .NET library: its Hugo site (docs/), built in a pinned Hugo image with the library's coverage
# report, and served by static-web-server. It is the image of the library's docs application (`<library>-docs` in
# the catalog, whose `repository` is the library and whose stack file is made from templates/stacks).
#
# The coverage report is not in the repository: it is the archive of the `coverage` check (`coverage-report/**`),
# which the pipeline hands to the deploy as an input (`deploy.inputs: [coverage]` in yggdrasil.yml), and
# scripts/deploy.sh copies into the build context's root before this builds. docs/hugo.toml mounts it:
#   [[module.mounts]]
#     source = 'coverage-report'
#     target = 'static/coverage-report'
# so the site serves it at <path>/coverage-report/.
#
# The build arguments are the stack file's (templates/stacks/site.yml), which scripts/deploy.sh feeds:
#   PUBLIC_HOST, PUBLIC_PATH   the address the site is served at, docs.<domain>/<library>: Hugo's baseURL is
#                    https://<host><path>/ (just <path>/ when there is no host). The proxy does not strip the
#                    path, so the files are put under it too. Leave the baseURL of docs/hugo.toml alone: this
#                    overrides it.
#   DOCS_DIR         the Hugo site's directory (default docs).
#   GIT_COMMIT, YGG_ENVIRONMENT   the commit and the environment this image is built for ({{ getenv
#                    "HUGO_GIT_COMMIT" }} and {{ getenv "HUGO_YGG_ENVIRONMENT" }} in a template: Hugo's getenv
#                    only allows names that start with HUGO_).
#
# The theme (Docsy: @docsy/theme in docs/package.json, themesDir = node_modules) is installed with `npm ci` from
# docs/package-lock.json. The image has Hugo extended, Node and npm, no Go and no git.
FROM hugomods/hugo:debian-node-lts-0.165.0@sha256:eef6a12572c8cf3fea72fb034606f3e6a0821368682139378608eabc94e67c87 AS build

WORKDIR /src
COPY . .

ARG DOCS_DIR=docs
# Fails here, saying so, when the deploy did not hand over the coverage report.
COPY coverage-report/ ${DOCS_DIR}/coverage-report/

ARG PUBLIC_HOST=
ARG PUBLIC_PATH=
ARG GIT_COMMIT=
ARG YGG_ENVIRONMENT=
ENV HUGO_GIT_COMMIT=${GIT_COMMIT} \
    HUGO_YGG_ENVIRONMENT=${YGG_ENVIRONMENT}

RUN set -eu; \
    cd "$DOCS_DIR"; \
    if [ -f package-lock.json ]; then npm ci; fi; \
    if [ -n "$PUBLIC_HOST" ]; then base="https://$PUBLIC_HOST$PUBLIC_PATH/"; else base="$PUBLIC_PATH/"; fi; \
    hugo --environment production --minify --gc --baseURL "$base" --destination /out

FROM joseluisq/static-web-server:2.44.1@sha256:94618e3d17802265a21926217e6f9e7832b311b6fbf5933684001739fafed06a

# Declared again: an argument lives in the stage that declares it.
ARG PUBLIC_PATH=
ARG GIT_COMMIT=
ARG YGG_ENVIRONMENT=

# The server has no shell, so nothing is run here: the site is copied under its path, and /healthz is a file of
# its own at the root. Port 8080, a numeric non-root user.
COPY --from=build /out/ /public${PUBLIC_PATH}/
COPY <<EOF /public/healthz
ok
EOF

# Hugo writes 404.html at the site's root (under the path): an address the site does not have is a 404.
ENV SERVER_PORT=8080 \
    SERVER_ROOT=/public \
    SERVER_ERROR_PAGE_404=/public${PUBLIC_PATH}/404.html

LABEL org.opencontainers.image.revision="${GIT_COMMIT}" \
      yggdrasil.built-for="${YGG_ENVIRONMENT}"

USER 65532:65532
EXPOSE 8080
