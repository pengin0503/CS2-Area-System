import {describe,it,expect} from 'vitest';
import {localize,readableKey} from './localization';
describe('localization fallback',()=>{
 it.each([
  ['CS2AS.Summary.AnchorRequired','Anchor required'],
  ['CS2AS.State.ModuleUnavailable','Module unavailable'],
  ['CS2AS.Tab.Overview','Overview'],
  ['MyModule.Tab.traffic_flow','Traffic flow'],
  ['CS2AS.Tab.Level2Details','Level 2 details'],
  ['CS2AS.UI.AssignHQ','Assign HQ'],
  ['plain','Plain'],
 ])('%s -> %s',(key,text)=>expect(readableKey(key)).toBe(text));
 it('uses the translation when present and the readable fallback for null, empty or echoed keys',()=>{
  expect(localize(()=>'翻訳','CS2AS.Tab.Overview')).toBe('翻訳');
  expect(localize(()=>null,'CS2AS.Tab.Overview')).toBe('Overview');
  expect(localize(()=>'','CS2AS.Tab.Overview')).toBe('Overview');
  expect(localize(k=>k,'CS2AS.Tab.Overview')).toBe('Overview');
 });
});
