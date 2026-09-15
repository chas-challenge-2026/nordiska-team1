import axiosInstance from "./axiosInstance";

export async function putEmail(id: string, email: string) {
    const res = await axiosInstance.put("/customers", {
        id: id,
        email: email
    })

    return res.data;
}