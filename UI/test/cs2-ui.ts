import React from 'react';
// Minimal stand-in for the game's Button: onSelect fires on click; a disabled button ignores clicks.
export function Button({onSelect,selected,disabled,children}:React.PropsWithChildren<{onSelect?:()=>void;selected?:boolean;disabled?:boolean}>){
 return React.createElement('button',{onClick:onSelect,'aria-pressed':selected,disabled},children);
}
