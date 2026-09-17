import axiosInstance from "./axiosInstance";

type UpdateCustomerData = {
    id: number;
    email?: string;
    phone?: string;
};

export async function updateCustomerData(data: UpdateCustomerData) {
    const res = await axiosInstance.patch("/customers", data);
    return res.data;
}