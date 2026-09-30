import React from 'react';
// A render/binding failure hides only the wrapped part. Retries use a capped exponential backoff so a transient
// binding error recovers in the same session without a render loop or console flood; retries never trigger actions.
export const PANEL_RETRY_BASE_MS=1000;
export const PANEL_RETRY_MAX_MS=30000;
export class PanelBoundary extends React.Component<React.PropsWithChildren,{failed:boolean}>{
 state={failed:false};
 private failures=0;
 private retryTimer:ReturnType<typeof setTimeout>|undefined;
 private stableTimer:ReturnType<typeof setTimeout>|undefined;
 static getDerivedStateFromError(){return {failed:true};}
 componentDidCatch(){
  clearTimeout(this.stableTimer);clearTimeout(this.retryTimer);
  const delay=Math.min(PANEL_RETRY_BASE_MS*2**this.failures,PANEL_RETRY_MAX_MS);
  this.failures++;
  this.retryTimer=setTimeout(()=>this.setState({failed:false}),delay);
 }
 componentDidUpdate(_:React.PropsWithChildren,previous:{failed:boolean}){
  // The backoff resets only after the recovered content stays healthy for the maximum delay.
  if(previous.failed&&!this.state.failed)this.stableTimer=setTimeout(()=>{this.failures=0;},PANEL_RETRY_MAX_MS);
 }
 componentWillUnmount(){clearTimeout(this.retryTimer);clearTimeout(this.stableTimer);}
 render(){return this.state.failed?null:this.props.children;}
}
