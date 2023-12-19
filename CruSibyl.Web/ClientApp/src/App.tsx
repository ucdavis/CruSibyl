import React, { useState } from "react";
import { Route, Routes } from "react-router-dom";
import AppRoutes from "./AppRoutes";
import { Layout } from "./components/Layout";
import "./custom.css";
import { AppContextShape, Permission, User } from "./types";
import AppContext from "./shared/AppContext";

//TODO: uncomment this when we have actual data
//declare var CruSibyl: AppContextShape;
var CruSibyl = {
  antiForgeryToken: "",
  user: {
    detail: {} as User,
    permissions: [] as Permission[],
  },
} as AppContextShape;

const App = () => {
  const [context, setContext] = useState<AppContextShape>(CruSibyl);

  return (
    <AppContext.Provider value={[context, setContext]}>
      <Layout>
        <Routes>
          {AppRoutes.map((route, index) => {
            const { element, ...rest } = route;
            return <Route key={index} {...rest} element={element} />;
          })}
        </Routes>
      </Layout>
    </AppContext.Provider>
  );
};

export default App;
