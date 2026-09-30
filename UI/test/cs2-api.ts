export function bindValue<T>(group:string,name:string,fallback?:T){return{value:fallback};}
export function useValue<T>(binding:{value:T}):T{return binding.value;}
export function trigger(){throw new Error('Test must explicitly provide binding behavior');}
