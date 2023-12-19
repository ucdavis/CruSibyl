import React from "react";
import { AppContextShape, User, Permission } from "../types";

const AppContext = React.createContext<
  [AppContextShape, React.Dispatch<React.SetStateAction<AppContextShape>>]
>([
  {
    antiForgeryToken: "",
    user: {
      detail: {} as User,
      permissions: [] as Permission[],
    },
  },
  () => {},
]);

export default AppContext;
