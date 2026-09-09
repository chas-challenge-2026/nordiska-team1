import axios from "axios";

const baseURL = import.meta.env.VITE_API_BASE_URL || "/api";

const axiosInstance = axios.create({
    baseURL,
    timeout: 10000,
    withCredentials: true,
    headers: {
        "Content-Type": "application/json",
        Accept: "application/json",
    },
});

export default axiosInstance;
