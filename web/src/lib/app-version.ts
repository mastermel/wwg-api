declare const __APP_VERSION__: string;

/** The deployed version (the image tag), or "dev" when not built by the Docker image build. */
export const appVersion: string = __APP_VERSION__;
