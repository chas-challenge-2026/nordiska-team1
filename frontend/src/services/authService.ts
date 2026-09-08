import axiosInstance from "./axiosInstance";

export default async function login(email: string, password: string) {
    try {
        const res = await axiosInstance.post("/auth/login", {email, password})
        sessionStorage.setItem("token", res.data.token);
    } catch (error) {
        console.error(error);
    }

}
