// Keep browser requests on the current origin. Next.js proxies /api to the
// private API container, so local, Cloudflare, and production URLs all work.
const base=process.env.NEXT_PUBLIC_API_URL??'/api';
export type Session={accessToken:string;expiresAt:string;user:{id:string;firstName:string;lastName:string;email:string;role:string;profilePhotoUrl?:string|null}};
export const getSession=()=>typeof window==='undefined'?null:JSON.parse(localStorage.getItem('session')??'null') as Session|null;
export const setSession=(x:Session|null)=>x?localStorage.setItem('session',JSON.stringify(x)):localStorage.removeItem('session');
let refreshPromise:Promise<Session|null>|null=null;
async function refresh(){if(!refreshPromise)refreshPromise=fetch(`${base}/auth/refresh`,{method:'POST',credentials:'include'}).then(async r=>{if(!r.ok)return null;const session=await r.json() as Session;setSession(session);return session}).finally(()=>{refreshPromise=null});return refreshPromise;}
export async function api<T>(path:string,init:RequestInit={},retry=true){const s=getSession();const send=(token?:string)=>fetch(`${base}${path}`,{...init,credentials:'include',headers:{'Content-Type':'application/json',...(token?{Authorization:`Bearer ${token}`}:{}) ,...init.headers}});let res=await send(s?.accessToken);if(res.status===401&&retry&&path!='/auth/login'&&path!='/auth/register'){const renewed=await refresh();if(renewed)res=await send(renewed.accessToken)}if(res.status===401){setSession(null);if(typeof window!=='undefined')location.href='/login';throw new Error('Session expired');}if(!res.ok)throw new Error((await res.json().catch(()=>({}))).message??'Something went wrong');return(res.status===204?undefined:await res.json()) as T;}
export async function logout(){try{await api('/auth/logout',{method:'POST'},false)}finally{setSession(null)}}
