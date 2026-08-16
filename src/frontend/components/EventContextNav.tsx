import Link from 'next/link';

export default function EventContextNav({eventId}:{eventId:string}){
 if(!eventId)return null;
 return <nav className="event-context-nav" aria-label="Event navigation">
  <Link href={`/events?eventId=${encodeURIComponent(eventId)}`}><span aria-hidden="true">←</span> Back to event overview</Link>
 </nav>;
}
