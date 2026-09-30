// Readable fallback for a localization key the active locale does not provide (for example a key supplied
// by a module without entries for this locale): "CS2AS.Summary.AnchorRequired" -> "Anchor required".
export function readableKey(key:string):string{
 const last=key.split('.').filter(Boolean).pop()??key;
 const words=last.match(/[A-Z]+(?![a-z])|[A-Z]?[a-z]+|\d+/g);
 if(!words)return key;
 const text=words.map(word=>word.length>1&&word===word.toUpperCase()?word:word.toLowerCase()).join(' ');
 return text.charAt(0).toUpperCase()+text.slice(1);
}
export function localize(translate:(key:string)=>string|null,key:string):string{
 const value=translate(key);
 return value==null||value===''||value===key?readableKey(key):value;
}
