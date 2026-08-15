import './globals.css';import './invoices.css';import './team.css';import './settings.css';import './access.css';import './events.css';import './event-costs.css';import './logistics.css';import './dashboard.css';import './profile.css';import './purchasing.css';import './responsive-fixes.css';
export const metadata={title:'WebWaves Digital',description:'Connected event operations for Sri Lankan businesses'};
export const viewport={width:'device-width',initialScale:1};
export default function RootLayout({children}:{children:React.ReactNode}){return <html lang="en"><body>{children}</body></html>}
