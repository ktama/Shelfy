import { mount } from "svelte";
import App from "./lib/App.svelte";
import "./lib/styles/app.css";

mount(App, { target: document.getElementById("app")! });
