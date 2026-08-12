import './globals.css';import './invoices.css';import './team.css';import './settings.css';
export const metadata={title:'LankaSaaS',description:'Simple business management for Sri Lankan SMEs'};
export default function RootLayout({children}:{children:React.ReactNode}){return <html lang="en"><body>{children}</body></html>}
