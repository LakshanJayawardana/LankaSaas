import type { NextConfig } from 'next';
const securityHeaders=[
 {key:'X-Content-Type-Options',value:'nosniff'},
 {key:'X-Frame-Options',value:'DENY'},
 {key:'Referrer-Policy',value:'no-referrer'},
 {key:'Permissions-Policy',value:'camera=(), microphone=(), geolocation=(self)'}
];
const config: NextConfig = {
 output:'standalone',
 async headers(){return[{source:'/(.*)',headers:securityHeaders}]},
 async rewrites(){
  return process.env.API_INTERNAL_URL
   ? [{source:'/api/:path*',destination:`${process.env.API_INTERNAL_URL}/api/:path*`}]
   : [];
 }
};
export default config;
