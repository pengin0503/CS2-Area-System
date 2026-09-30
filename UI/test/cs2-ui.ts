import React from 'react';
// Minimal stand-in for the game's Button: onSelect fires on click.
export function Button({onSelect,selected,children}:React.PropsWithChildren<{onSelect?:()=>void;selected?:boolean}>){
 return React.createElement('button',{onClick:onSelect,'aria-pressed':selected},children);
}
