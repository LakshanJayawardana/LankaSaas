import './globals.css';import './invoices.css';import './team.css';import './settings.css';import './access.css';import './events.css';import './event-costs.css';import './logistics.css';import './dashboard.css';import './profile.css';import './purchasing.css';import './mobile.css';import './fixed-sidebar.css';
export const metadata={title:'LankaSaaS',description:'Simple business management for Sri Lankan SMEs'};
export const viewport={width:'device-width',initialScale:1};
export default function RootLayout({children}:{children:React.ReactNode}){return <html lang="en"><body>{children}</body></html>}
