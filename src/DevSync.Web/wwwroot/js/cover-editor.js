(() => {
    document.querySelectorAll('[data-cover-editor]').forEach(section => {
        const form=section.closest('form'),input=section.querySelector('[data-cover-id]'),file=section.querySelector('[data-cover-file]');
        const image=section.querySelector('[data-cover-preview]'),remove=section.querySelector('[data-cover-remove]'),status=section.querySelector('[data-cover-status]');
        if(!form||!input||!file||!image||!remove||!status) return;
        let uploading=false;
        function select(id) {
            input.value=id; image.hidden=!id; remove.hidden=!id;
            if(id) image.src='/media/images/'+id; else image.removeAttribute('src');
            input.dispatchEvent(new Event('input',{bubbles:true}));
        }
        // File selection itself is not a document edit; mark dirty only after upload succeeds.
        file.addEventListener('input',event=>event.stopPropagation());
        file.addEventListener('change',async event=>{
            event.stopPropagation(); const selected=file.files?.[0]; if(!selected||uploading) return;
            if(selected.size<=0||selected.size>5*1024*1024) {status.textContent='Choose a PNG or JPEG up to 5 MB. Your previous cover is unchanged.';file.value='';return;}
            uploading=true;file.disabled=true;remove.disabled=true;status.textContent='Uploading cover… Wait for the saved confirmation before leaving.';
            try {
                const data=new FormData();data.append('image',selected);
                data.append('__RequestVerificationToken',form.querySelector('[name="__RequestVerificationToken"]').value);
                const response=await fetch('/images/cover-upload',{method:'POST',body:data,headers:{Accept:'application/json'}});
                if(response.redirected||response.status===401||response.status===403) throw new Error('Your session changed. Sign in again before uploading.');
                if(!response.headers.get('content-type')?.includes('application/json')) throw new Error('Upload failed. Try a PNG or JPEG up to 5 MB.');
                const result=await response.json();
                if(!response.ok) throw new Error(result.error||'Unable to upload this image.');
                if(!/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(result.id||'')) throw new Error('Upload could not be confirmed. Try again.');
                select(result.id);status.textContent='Image uploaded to My images. Save this document, or wait for autosave, to keep this cover.';
            } catch(error) {status.textContent=(error.message||'Upload failed.')+' Your previous cover is unchanged.';}
            finally {uploading=false;file.disabled=false;remove.disabled=false;file.value='';}
        });
        remove.addEventListener('click',()=>{if(uploading) return;select('');status.textContent='Cover removed. Save this document, or wait for autosave, to keep this change.';});
        form.addEventListener('submit',event=>{if(uploading){event.preventDefault();event.stopImmediatePropagation();status.textContent='Wait for the cover upload to finish, then save again.';}},true);
        window.addEventListener('beforeunload',event=>{if(uploading){event.preventDefault();event.returnValue='';}});
    });
})();
