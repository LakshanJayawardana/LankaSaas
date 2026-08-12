export type InvoiceItem={id?:string;productId?:string|null;description:string;quantity:number;unitPrice:number;discount:number;taxRate:number;lineSubtotal?:number;lineTotal?:number};
export type Invoice={id:string;invoiceNumber:string;customerId:string;customerName:string;issueDate:string;dueDate:string;status:string;subtotal:number;discountTotal:number;taxTotal:number;total:number;notes?:string;items:InvoiceItem[]};
export type InvoiceList={id:string;invoiceNumber:string;customerName:string;issueDate:string;dueDate:string;status:string;total:number};
export type Customer={id:string;name:string};export type Product={id:string;name:string;sku:string;sellingPrice:number};
export const money=(n:number)=>`LKR ${n.toLocaleString(undefined,{minimumFractionDigits:2,maximumFractionDigits:2})}`;
