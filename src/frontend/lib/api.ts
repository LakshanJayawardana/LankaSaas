const base=process.env.NEXT_PUBLIC_API_URL??'http://localhost:8080/api';
export type Session={accessToken:string;refreshToken:string;expiresAt:string;user:{firstName:string;lastName:string;email:string;role:string}};
export const getSession=()=>typeof window==='undefined'?null:JSON.parse(localStorage.getItem('session')??'null') as Session|null;
export const setSession=(x:Session|null)=>x?localStorage.setItem('session',JSON.stringify(x)):localStorage.removeItem('session');
export async function api<T>(path:string,init:RequestInit={}){const s=getSession();const res=await fetch(`${base}${path}`,{...init,headers:{'Content-Type':'application/json',...(s?{Authorization:`Bearer ${s.accessToken}`}:{}) ,...init.headers}});if(res.status===401&&s){setSession(null);location.href='/login';throw new Error('Session expired');}if(!res.ok)throw new Error((await res.json().catch(()=>({}))).message??'Something went wrong');return(res.status===204?undefined:await res.json()) as T;}
