# Customization

## Login button

The button is plain HTML in **Dashboard → General → Login disclaimer**, styled by **Dashboard → General → Custom CSS code**. A basic version is in the [README](../README.md#3-add-a-login-button). Some variations:

### Logo inside the button

```html
<form action="/authentik/login" class="sso-login-form">
  <button type="submit" class="sso-login-btn">
    <img src="https://example.com/logo.svg" alt="" class="sso-login-logo">
    Sign in with Authentik
  </button>
</form>
```

```css
.sso-login-btn { display: inline-flex; align-items: center; justify-content: center; gap: 0.5em; }
.sso-login-logo { width: 20px; height: 20px; object-fit: contain; }
```

### Authentik colors

```css
.sso-login-btn { background: #fd4b2d; }
.sso-login-btn:hover { background: #e0432a; }
```

### Full-width, rounded

```css
.sso-login-btn { max-width: 100%; border-radius: 2em; padding: 1em; }
```

## Callback page

After Authentik redirects back, a short "Completing login…" page finishes the sign-in. It follows the browser's light/dark preference and also loads Jellyfin's Custom CSS, so you can restyle it:

```css
.sso-container { background: #1a1a2e; }
.sso-spinner { border-top-color: #e94560; }
.sso-error { color: #ff6b6b; }
```

## Profile pictures from a custom attribute

If avatars are stored in a user attribute (for example `attributes.photo`) instead of Authentik's built-in avatar, use this scope mapping expression:

```python
return {"photo": request.user.attributes.get("photo", "")}
```

and set **Profile Image Claim Path** to `photo`. The claim path supports dot notation for nested values (for example `attributes.photo` if your mapping returns the whole `attributes` object).

The claim may contain either an image URL or a `data:` URI (PNG, JPEG, GIF or WebP). When profile sync is on and the claim is empty, the user's Jellyfin picture is cleared so the default avatar is shown.
