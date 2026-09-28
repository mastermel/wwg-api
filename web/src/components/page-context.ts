import { createContext } from "react";

/**
 * True inside the sign-in card (PublicLayout): pages there get a compact header, without the
 * divider and page-level actions of the app frame.
 */
export const CompactPageContext = createContext(false);
