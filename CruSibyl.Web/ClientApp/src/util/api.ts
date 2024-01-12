import { AppContextShape } from "../types";

declare var CruSibyl: AppContextShape;

export const AuthenticatedFetch = async (
  url: string,
  init?: RequestInit,
  additionalHeaders?: HeadersInit
): Promise<any> =>
  fetch(url, {
    ...init,
    credentials: "include",
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json",
      RequestVerificationToken: CruSibyl.antiForgeryToken,
      ...additionalHeaders,
    },
  });
