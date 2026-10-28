import { format } from 'prettier/standalone';

const aliases={ 'c#':'csharp',cs:'csharp','csharp':'csharp',js:'javascript',javascript:'javascript',ts:'typescript',typescript:'typescript',json:'json',html:'html',css:'css',sql:'sql',py:'python',python:'python',xml:'xml',yaml:'yaml',yml:'yaml',md:'markdown',markdown:'markdown',text:'plaintext',plaintext:'plaintext' };
export const languageId = value => aliases[(value||'').trim().toLowerCase()] || 'plaintext';
export const canFormat = language => ['csharp','javascript','typescript','json','html','css'].includes(language);
export async function formatLocalCode(language,code) {
    let parser,plugins;
    switch(language) {
        case 'javascript': parser='babel'; plugins=[await import('prettier/plugins/babel'),await import('prettier/plugins/estree')]; break;
        case 'typescript': parser='typescript'; plugins=[await import('prettier/plugins/typescript'),await import('prettier/plugins/estree')]; break;
        case 'json': parser='json'; plugins=[await import('prettier/plugins/babel'),await import('prettier/plugins/estree')]; break;
        case 'html': parser='html'; plugins=[await import('prettier/plugins/html')]; break;
        case 'css': parser='css'; plugins=[await import('prettier/plugins/postcss')]; break;
        default: throw new Error('Formatting supports C#, JavaScript, TypeScript, JSON, HTML, and CSS.');
    }
    const output=await format(code,{parser,plugins,tabWidth:2,endOfLine:'lf'});
    if(output.length>60000) throw new Error('Formatted code exceeds the 60,000-character limit.');
    return output;
}
// Formatting is asynchronous: newer typing, language changes and removed blocks must win.
export async function formatIfCurrent(target,formatter) {
    const source=target.getValue(),version=target.getVersion(),language=target.getLanguage();
    const result=await formatter(language,source);
    if(!target.isAlive() || target.getVersion()!==version || target.getLanguage()!==language) return false;
    if(typeof result!=='string' || result.length>60000) throw new Error('The formatted code could not be applied.');
    if(result!==source) target.apply(result);
    return true;
}
