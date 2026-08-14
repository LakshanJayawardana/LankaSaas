const base=process.env.NEXT_PUBLIC_API_URL??'/api';

export type PlatformSession={accessToken:string;expiresAt:string;email:string;role:string};
export const getPlatformSession=()=>typeof window==='undefined'?null:JSON.parse(localStorage.getItem('platformSession')??'null') as PlatformSession|null;
export const setPlatformSession=(session:PlatformSession|null)=>session?localStorage.setItem('platformSession',JSON.stringify(session)):localStorage.removeItem('platformSession');

export async function platformApi<T>(path:string,init:RequestInit={}){
 const session=getPlatformSession();
 const response=await fetch(`${base}/platform${path}`,{...init,headers:{'Content-Type':'application/json',...(session?{Authorization:`Bearer ${session.accessToken}`}:{}) ,...init.headers}});
 if(response.status===401){setPlatformSession(null);if(path==='/auth/login')throw new Error('Invalid platform email or password');if(typeof window!=='undefined'&&location.pathname!=='/platform/login')location.href='/platform/login';throw new Error('Platform session expired');}
 if(!response.ok){const problem=await response.json().catch(()=>({})) as {message?:string;detail?:string;title?:string;errors?:Record<string,string[]>};const validation=problem.errors?Object.values(problem.errors).flat()[0]:undefined;throw new Error(problem.message??validation??problem.detail??problem.title??'Something went wrong');}
 return(response.status===204?undefined:await response.json()) as T;
}
